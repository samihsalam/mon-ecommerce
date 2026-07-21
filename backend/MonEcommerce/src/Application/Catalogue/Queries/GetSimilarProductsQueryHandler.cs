using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetSimilarProductsQueryHandler : IRequestHandler<GetSimilarProductsQuery, List<ProductSummaryDto>>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetSimilarProductsQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    public Task<List<ProductSummaryDto>> Handle(GetSimilarProductsQuery request, CancellationToken cancellationToken)
        => _catalogueService.GetSimilarProductsAsync(request.ProductId, cancellationToken);
}
