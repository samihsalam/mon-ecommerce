using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetCategoriesQueryHandler : IRequestHandler<GetCategoriesQuery, List<CategorySummaryDto>>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetCategoriesQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    public Task<List<CategorySummaryDto>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        => _catalogueService.GetCategoriesAsync(cancellationToken);
}
