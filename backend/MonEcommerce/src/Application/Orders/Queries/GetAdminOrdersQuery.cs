using MonEcommerce.Application.Common.Security;
using MonEcommerce.Application.Orders.Models;
using MonEcommerce.Domain.Constants;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Orders.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetAdminOrdersQuery(
    OrderStatus? Status,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? Search,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedOrdersResult<AdminOrderSummaryDto>>;
