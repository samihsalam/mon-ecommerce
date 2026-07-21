using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

// No [Authorize] — public product detail page, same as every other catalogue query.
public record GetProductByIdQuery(Guid Id) : IRequest<ProductDetailDto>;
