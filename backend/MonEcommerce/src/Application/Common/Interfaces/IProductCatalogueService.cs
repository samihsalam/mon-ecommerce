using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IProductCatalogueService
{
    Task<PagedProductsResult<ProductSummaryDto>> GetProductsAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    // Throws NotFoundException (mapped to 404 by ProblemDetailsExceptionHandler) when the id
    // doesn't exist OR the product isn't published — same "published only" interpretation as
    // GetProductsAsync, so an unpublished product's detail page can't be reached by guessing its id.
    Task<ProductDetailDto> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // term is expected to already be validated (2+ chars) by GetSearchSuggestionsQueryValidator.
    Task<SuggestionsResult> GetSearchSuggestionsAsync(string term, CancellationToken cancellationToken = default);

    Task<List<CategorySummaryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    // Returns an empty list (not NotFoundException) when productId doesn't exist or isn't
    // published — unlike GetProductByIdAsync, a missing/unpublished source product just means
    // "no similar products to show," not a 404-worthy request.
    Task<List<ProductSummaryDto>> GetSimilarProductsAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<List<SitemapEntryDto>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default);

    // Not called by anything yet — no product create/update/delete endpoint exists (Epic 6).
    // Ready for those future command handlers to call directly via DI.
    Task InvalidateCatalogueCacheAsync(CancellationToken cancellationToken = default);
}
