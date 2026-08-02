using MonEcommerce.Application.Common.Security;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Constants;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetAdminReturnsQuery(ReturnStatus? Status, DateTimeOffset? DateFrom, DateTimeOffset? DateTo) : IRequest<List<AdminReturnSummaryDto>>;
