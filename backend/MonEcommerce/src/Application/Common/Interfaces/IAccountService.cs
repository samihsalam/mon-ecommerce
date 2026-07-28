using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAccountService
{
    Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProfileDto>> UpdateProfileAsync(string userId, string name, string email, string? currentPassword, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> GetOrderDetailAsync(string userId, Guid orderId, CancellationToken cancellationToken = default);

    // Story 4.6's checkout confirmation page polls this by the Stripe PaymentIntent id (the only
    // identifier the browser has — order creation happens asynchronously via webhook, so there is
    // no Order.Id to poll by yet). Throws ConflictException if a PaymentAuditLog shows the payment
    // was refunded for insufficient stock instead, or NotFoundException while still pending (the
    // webhook hasn't landed yet) — both distinguishable by HTTP status for the frontend's poll loop.
    Task<OrderDetailDto> GetOrderByPaymentIntentAsync(string userId, string paymentIntentId, CancellationToken cancellationToken = default);
}
