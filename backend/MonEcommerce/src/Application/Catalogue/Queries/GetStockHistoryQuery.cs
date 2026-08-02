using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetStockHistoryQuery(Guid ProductId) : IRequest<List<StockMovementDto>>;
