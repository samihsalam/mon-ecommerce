using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAccountService
{
    Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default);
    Task<Result<ProfileDto>> UpdateProfileAsync(string userId, string name, string email, string? currentPassword, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> GetOrderDetailAsync(string userId, Guid orderId, CancellationToken cancellationToken = default);
}
