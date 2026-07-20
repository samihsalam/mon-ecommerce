using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

// No [Authorize] — the first genuinely public (unauthenticated) query in this codebase.
public record GetProductsQuery(
    Guid? CategoryId,
    string? Material,
    string? Color,
    int? PriceMin,
    int? PriceMax,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedProductsResult<ProductSummaryDto>>;
