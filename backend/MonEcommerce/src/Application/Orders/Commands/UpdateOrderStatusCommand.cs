using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Orders.Commands;

// The minimal admin-only slice of order-status management this story needs to make AC #1/#2
// triggerable at all — Story 7.2 owns the real admin UI/workflow built on top of this same
// command. See Story 5.2's Dev Notes.
[Authorize(Roles = Roles.Administrator)]
public record UpdateOrderStatusCommand(Guid OrderId, OrderStatus NewStatus, string? TrackingNumber) : IRequest;
