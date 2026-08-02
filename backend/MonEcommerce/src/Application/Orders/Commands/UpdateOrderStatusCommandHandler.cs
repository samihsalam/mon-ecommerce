using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.Orders.Commands;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand>
{
    // AC #5/#6: strict "next stage only" — no skipping ahead, no staying at the same status, no
    // going backward. Cancellation is only reachable from Pending/Processing (AC #6) simply
    // because those are the only two entries that list Cancelled as a valid next status; Shipped/
    // Delivered/Cancelled are otherwise terminal or near-terminal with no way back.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IConfiguration _configuration;

    public UpdateOrderStatusCommandHandler(IApplicationDbContext context, IIdentityService identityService, IConfiguration configuration)
    {
        _context = context;
        _identityService = identityService;
        _configuration = configuration;
    }

    public async Task Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        // No user-scoping — an admin acts across every customer's orders, unlike every
        // customer-facing lookup elsewhere in this codebase.
        var order = await _context.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Order), request.OrderId);

        var previousStatus = order.Status;
        var allowedNextStatuses = ValidTransitions[previousStatus];

        if (!allowedNextStatuses.Contains(request.NewStatus))
        {
            var fromLabel = OrderStatusLabelFormatter.Format(previousStatus);
            var toLabel = OrderStatusLabelFormatter.Format(request.NewStatus);
            var validLabels = allowedNextStatuses.Select(OrderStatusLabelFormatter.Format).ToList();
            var detail = validLabels.Count > 0
                ? $"Transition invalide : {fromLabel} → {toLabel}. Transitions valides depuis {fromLabel} : {string.Join(", ", validLabels)}."
                : $"Transition invalide : {fromLabel} → {toLabel}. Aucune transition n'est possible depuis le statut {fromLabel}.";

            throw new AppValidationException(
            [
                new FluentValidation.Results.ValidationFailure(nameof(request.NewStatus), detail),
            ]);
        }

        order.Status = request.NewStatus;
        if (request.TrackingNumber != null)
        {
            order.TrackingNumber = request.TrackingNumber;
        }

        // AC #4: previous status, new status, admin user ID + timestamp — the latter two come
        // free from BaseAuditableEntity's CreatedBy/Created via AuditableEntityInterceptor, same
        // convention as StockMovement (Story 6.4).
        _context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = request.NewStatus,
        });

        // Only these two transitions have an AC-defined email — no event for any other status
        // change (Story 5.2's scope is exactly AC #1/#2, not every possible transition).
        if (request.NewStatus == OrderStatus.Shipped || request.NewStatus == OrderStatus.Delivered)
        {
            var customerEmail = await _identityService.GetEmailAsync(order.UserId) ?? string.Empty;

            if (request.NewStatus == OrderStatus.Shipped)
            {
                var baseUrl = _configuration["Frontend:BaseUrl"]!.TrimEnd('/');
                var trackingLink = $"{baseUrl}/compte/commandes/{order.Id}";
                order.AddDomainEvent(new OrderShippedEvent(order.Id, customerEmail, order.TrackingNumber ?? string.Empty, trackingLink));
            }
            else
            {
                order.AddDomainEvent(new OrderDeliveredEvent(order.Id, customerEmail));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
