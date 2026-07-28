using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Shipping;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Payments.Webhooks;

public class HandleStripeWebhookCommandHandler : IRequestHandler<HandleStripeWebhookCommand>
{
    private const string PaymentIntentSucceeded = "payment_intent.succeeded";
    private const int MaxStockRetries = 3;

    private readonly IPaymentService _paymentService;
    private readonly ICartService _cartService;
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IPublisher _publisher;
    private readonly ILogger<HandleStripeWebhookCommandHandler> _logger;

    public HandleStripeWebhookCommandHandler(
        IPaymentService paymentService,
        ICartService cartService,
        IApplicationDbContext context,
        IIdentityService identityService,
        IPublisher publisher,
        ILogger<HandleStripeWebhookCommandHandler> logger)
    {
        _paymentService = paymentService;
        _cartService = cartService;
        _context = context;
        _identityService = identityService;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(HandleStripeWebhookCommand request, CancellationToken cancellationToken)
    {
        var webhookEvent = _paymentService.ParseWebhookEvent(request.Payload, request.SignatureHeader);

        if (webhookEvent.Type != PaymentIntentSucceeded || webhookEvent.PaymentIntentId == null)
        {
            // Stripe sends many event types to the same webhook URL — only this one is relevant
            // here. Returning normally (200 OK at the endpoint level) is correct, not a gap:
            // Stripe would otherwise retry indefinitely for event types we never act on.
            return;
        }

        var paymentIntentId = webhookEvent.PaymentIntentId;

        // Idempotency guard (Stripe redelivers on any non-2xx response or timeout) — without
        // this, a retried delivery of the same payment_intent.succeeded could double-decrement
        // stock or double-create an order for one payment.
        var alreadyProcessed = await _context.PaymentAuditLogs
            .AnyAsync(p => p.StripePaymentIntentId == paymentIntentId, cancellationToken);
        if (alreadyProcessed)
        {
            return;
        }

        var metadata = webhookEvent.Metadata;
        if (!metadata.TryGetValue("userId", out var userId) ||
            !metadata.TryGetValue("shippingAddressId", out var shippingAddressIdRaw) ||
            !metadata.TryGetValue("shippingOptionId", out var shippingOptionId) ||
            !Guid.TryParse(shippingAddressIdRaw, out var shippingAddressId) ||
            !ShippingOptionsCatalog.TryGetById(shippingOptionId, out var shippingOption))
        {
            // Malformed/missing metadata means this PaymentIntent wasn't created by
            // CreatePaymentIntentCommandHandler (Story 4.5/4.6) — nothing safe to reconstruct an
            // order from. Logged, not thrown: a 500 here would make Stripe retry forever for a
            // payload that will never become processable.
            _logger.LogError("Stripe webhook: PaymentIntent {PaymentIntentId} is missing expected metadata; skipping.", paymentIntentId);
            return;
        }

        var amountInCents = (int)(webhookEvent.AmountInCents ?? 0);
        var customerEmail = await _identityService.GetEmailAsync(userId);

        var cart = await _cartService.GetCartAsync(CartOwner.ForUser(userId), cancellationToken);

        if (cart.Items.Count == 0 || !await TryReserveStockAsync(cart.Items, cancellationToken))
        {
            await RefundAndNotifyAsync(paymentIntentId, userId, amountInCents, customerEmail, cancellationToken);
            return;
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            ShippingAddressId = shippingAddressId,
            TotalInCents = amountInCents,
            StripePaymentIntentId = paymentIntentId,
        };
        foreach (var item in cart.Items)
        {
            order.Items.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                UnitPriceInCents = item.UnitPriceInCents,
                Quantity = item.Quantity,
            });
        }
        _context.Orders.Add(order);

        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            Id = Guid.NewGuid(),
            StripePaymentIntentId = paymentIntentId,
            UserId = userId,
            AmountInCents = amountInCents,
            Outcome = PaymentAuditOutcome.Confirmed,
            OrderId = order.Id,
        });

        // AddDomainEvent + DispatchDomainEventsInterceptor: the established-but-previously-unused
        // pattern (Domain/Common/BaseEntity.cs) — dispatched on the same SaveChangesAsync below,
        // triggering OrderPlacedEmailHandler (already built, Story 1.x — untouched by this story).
        order.AddDomainEvent(new OrderPlacedEvent(order.Id, userId, customerEmail ?? string.Empty, order.TotalInCents));

        await _context.SaveChangesAsync(cancellationToken);

        await _cartService.ClearCartAsync(CartOwner.ForUser(userId), cancellationToken);
    }

    // Checks every cart item's Stock.Quantity and decrements it in one batch, atomically, using
    // Stock.RowVersion (SQL Server's rowversion) as an EF Core optimistic-concurrency token — the
    // SQL Server equivalent of the AC's literal "xmin PostgreSQL" wording (see Story 4.6's Dev
    // Notes). If a concurrent request changed a Stock row between our read and write, EF throws
    // DbUpdateConcurrencyException; reload the affected rows and re-check/re-decrement, bounded to
    // MaxStockRetries attempts before treating it as genuinely insufficient stock.
    private async Task<bool> TryReserveStockAsync(List<CartItemDto> items, CancellationToken cancellationToken)
    {
        var productIds = items.Select(i => i.ProductId).ToList();

        for (var attempt = 1; attempt <= MaxStockRetries; attempt++)
        {
            var stocks = await _context.Stocks
                .Where(s => productIds.Contains(s.ProductId))
                .ToListAsync(cancellationToken);

            var sufficient = items.All(item =>
                stocks.FirstOrDefault(s => s.ProductId == item.ProductId) is { } stock && stock.Quantity >= item.Quantity);

            if (!sufficient)
            {
                return false;
            }

            foreach (var item in items)
            {
                var stock = stocks.First(s => s.ProductId == item.ProductId);
                stock.Quantity -= item.Quantity;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                {
                    await entry.ReloadAsync(cancellationToken);
                }
                // Loop again — the reloaded entries now hold the database's current values, so
                // the next iteration's fresh query + sufficiency check reflects reality.
            }
        }

        return false;
    }

    private async Task RefundAndNotifyAsync(string paymentIntentId, string userId, int amountInCents, string? customerEmail, CancellationToken cancellationToken)
    {
        await _paymentService.CreateRefundAsync(paymentIntentId, amountInCents, cancellationToken);

        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            Id = Guid.NewGuid(),
            StripePaymentIntentId = paymentIntentId,
            UserId = userId,
            AmountInCents = amountInCents,
            Outcome = PaymentAuditOutcome.Refunded,
            OrderId = null,
        });

        await _context.SaveChangesAsync(cancellationToken);

        // Deliberately NOT RefundIssuedEvent — that event assumes an Order already exists
        // (Epic 5's post-delivery-return flow); no Order is ever created on this path (see
        // Domain/Events/StockUnavailableEvent.cs). No entity to attach this event to either
        // (there's no Order), so it's published directly via IPublisher — the same mechanism
        // AuthService already uses for UserRegisteredEvent/PasswordResetRequestedEvent (two
        // established, both-valid event-publish mechanisms in this codebase; see Dev Notes).
        await _publisher.Publish(new StockUnavailableEvent(customerEmail ?? string.Empty, amountInCents), cancellationToken);
    }
}
