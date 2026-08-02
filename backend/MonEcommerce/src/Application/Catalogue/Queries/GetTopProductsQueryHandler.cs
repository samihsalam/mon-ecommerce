using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetTopProductsQueryHandler : IRequestHandler<GetTopProductsQuery, TopProductsDto>
{
    private readonly IAnalyticsService _analyticsService;

    public GetTopProductsQueryHandler(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public Task<TopProductsDto> Handle(GetTopProductsQuery request, CancellationToken cancellationToken)
        => _analyticsService.GetTopProductsAsync(cancellationToken);
}
