using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Orders.Commands;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand>
{
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

        order.Status = request.NewStatus;
        if (request.TrackingNumber != null)
        {
            order.TrackingNumber = request.TrackingNumber;
        }

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
