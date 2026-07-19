using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Security;

namespace MonEcommerce.Application.Account.Queries;

[Authorize]
public record GetOrderDetailQuery(Guid OrderId) : IRequest<OrderDetailDto>;
