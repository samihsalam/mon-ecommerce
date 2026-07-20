using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IProductCatalogueService
{
    Task<PagedProductsResult<ProductSummaryDto>> GetProductsAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    // term is expected to already be validated (2+ chars) by GetSearchSuggestionsQueryValidator.
    Task<SuggestionsResult> GetSearchSuggestionsAsync(string term, CancellationToken cancellationToken = default);

    Task<List<CategorySummaryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    // Not called by anything yet — no product create/update/delete endpoint exists (Epic 6).
    // Ready for those future command handlers to call directly via DI.
    Task InvalidateCatalogueCacheAsync(CancellationToken cancellationToken = default);
}
