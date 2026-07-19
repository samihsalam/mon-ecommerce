using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Security;

namespace MonEcommerce.Application.Account.Queries;

[Authorize]
public record GetOrdersQuery(int Page = 1, int PageSize = 10) : IRequest<PagedResult<OrderSummaryDto>>;
