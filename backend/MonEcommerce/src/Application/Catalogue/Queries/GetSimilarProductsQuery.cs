using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

public record GetSimilarProductsQuery(Guid ProductId) : IRequest<List<ProductSummaryDto>>;
