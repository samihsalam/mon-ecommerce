using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetLowStockProductsQueryHandler : IRequestHandler<GetLowStockProductsQuery, List<LowStockProductDto>>
{
    private readonly IAnalyticsService _analyticsService;

    public GetLowStockProductsQueryHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<List<LowStockProductDto>> Handle(GetLowStockProductsQuery request, CancellationToken cancellationToken)
        => _analyticsService.GetLowStockProductsAsync(cancellationToken);
}
