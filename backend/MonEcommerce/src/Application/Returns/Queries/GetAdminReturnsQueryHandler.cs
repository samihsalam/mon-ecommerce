using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.Models;

namespace MonEcommerce.Application.Returns.Queries;

public class GetAdminReturnsQueryHandler : IRequestHandler<GetAdminReturnsQuery, List<AdminReturnSummaryDto>>
{
    private readonly IAdminReturnService _adminReturnService;

    public GetAdminReturnsQueryHandler(IAdminReturnService adminReturnService)
    {
        _adminReturnService = adminReturnService;
    }

    public Task<List<AdminReturnSummaryDto>> Handle(GetAdminReturnsQuery request, CancellationToken cancellationToken)
    {
        var filter = new AdminReturnFilter(request.Status, request.DateFrom, request.DateTo);
        return _adminReturnService.GetReturnsAsync(filter, cancellationToken);
    }
}
