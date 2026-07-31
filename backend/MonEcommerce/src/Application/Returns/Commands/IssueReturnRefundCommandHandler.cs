using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Returns.Commands;

public class IssueReturnRefundCommandHandler : IRequestHandler<IssueReturnRefundCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IPaymentService _paymentService;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;
    private readonly ILogger<IssueReturnRefundCommandHandler> _logger;

    public IssueReturnRefundCommandHandler(
        IApplicationDbContext context,
        IPaymentService paymentService,
        IIdentityService identityService,
        IUser user,
        ILogger<IssueReturnRefundCommandHandler> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _identityService = identityService;
        _user = user;
        _logger = logger;
    }

    public async Task Handle(IssueReturnRefundCommand request, CancellationToken cancellationToken)
    {
        // No user-scoping — admin-wide, same as every other admin command this epic added.
        var returnRequest = await _context.Returns
            .FirstOrDefaultAsync(r => r.Id == request.ReturnId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Return), request.ReturnId);

        if (returnRequest.Status != ReturnStatus.Approved)
        {
            throw new ConflictException("Cette demande de retour doit être validée avant de pouvoir être remboursée.");
        }

        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == returnRequest.OrderId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Order), returnRequest.OrderId);

        if (string.IsNullOrWhiteSpace(order.StripePaymentIntentId))
        {
            throw new ConflictException("Cette commande n'a pas de paiement Stripe associé.");
        }

        // AC #3: nothing is written to the tracked context — no SaveChangesAsync — until AFTER
        // this call succeeds. A local DB transaction can't "undo" a call that already reached
        // Stripe, so the "no partial state" guarantee comes from this ordering, not from
        // TransactionScope (same reasoning as HandleStripeWebhookCommandHandler's refund path,
        // Story 4.6).
        string stripeRefundId;
        try
        {
            stripeRefundId = await _paymentService.CreateRefundAsync(order.StripePaymentIntentId, order.TotalInCents, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "MonEcommerce Alert: Stripe refund failed for return {ReturnId} (order {OrderId})", returnRequest.Id, order.Id);
            throw new RefundFailedException("Le remboursement Stripe a échoué. Aucune modification n'a été enregistrée.", ex);
        }

        returnRequest.Status = ReturnStatus.Refunded;

        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            Id = Guid.NewGuid(),
            StripePaymentIntentId = order.StripePaymentIntentId,
            UserId = order.UserId,
            AmountInCents = order.TotalInCents,
            Outcome = PaymentAuditOutcome.Refunded,
            OrderId = order.Id,
            AdminUserId = _user.Id,
            StripeRefundId = stripeRefundId,
        });

        var customerEmail = await _identityService.GetEmailAsync(order.UserId) ?? string.Empty;
        returnRequest.AddDomainEvent(new RefundIssuedEvent(
            Guid.NewGuid(),
            order.Id,
            customerEmail,
            order.TotalInCents,
            OrderNumberFormatter.Format(order.Id)));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
