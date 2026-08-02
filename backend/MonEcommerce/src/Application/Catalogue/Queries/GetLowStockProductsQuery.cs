using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetLowStockProductsQuery : IRequest<List<LowStockProductDto>>;
