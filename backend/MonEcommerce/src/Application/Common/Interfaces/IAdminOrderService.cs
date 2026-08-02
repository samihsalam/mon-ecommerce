using MonEcommerce.Application.Orders.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAdminOrderService
{
    Task<PagedOrdersResult<AdminOrderSummaryDto>> GetOrdersAsync(AdminOrderFilter filter, CancellationToken cancellationToken = default);
}
