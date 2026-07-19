using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedProductsResult<ProductSummaryDto>>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetProductsQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    public Task<PagedProductsResult<ProductSummaryDto>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var filter = new ProductFilter(
            request.CategoryId,
            request.Material,
            request.Color,
            request.PriceMin,
            request.PriceMax,
            request.PageNumber,
            request.PageSize);

        return _catalogueService.GetProductsAsync(filter, cancellationToken);
    }
}
