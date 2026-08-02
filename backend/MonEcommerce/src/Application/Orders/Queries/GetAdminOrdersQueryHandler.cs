using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.Models;

namespace MonEcommerce.Application.Orders.Queries;

public class GetAdminOrdersQueryHandler : IRequestHandler<GetAdminOrdersQuery, PagedOrdersResult<AdminOrderSummaryDto>>
{
    private readonly IAdminOrderService _adminOrderService;

    public GetAdminOrdersQueryHandler(IAdminOrderService adminOrderService)
    {
        _adminOrderService = adminOrderService;
    }

    public Task<PagedOrdersResult<AdminOrderSummaryDto>> Handle(GetAdminOrdersQuery request, CancellationToken cancellationToken)
    {
        var filter = new AdminOrderFilter(
            request.Status,
            request.DateFrom,
            request.DateTo,
            request.Search,
            request.PageNumber,
            request.PageSize);

        return _adminOrderService.GetOrdersAsync(filter, cancellationToken);
    }
}
