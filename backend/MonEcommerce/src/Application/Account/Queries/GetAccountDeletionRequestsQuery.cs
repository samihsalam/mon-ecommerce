using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Account.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetAccountDeletionRequestsQuery : IRequest<List<AccountDeletionRequestDto>>;
