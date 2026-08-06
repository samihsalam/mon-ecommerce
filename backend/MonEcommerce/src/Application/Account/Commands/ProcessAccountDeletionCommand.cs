using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Account.Commands;

[Authorize(Roles = Roles.Administrator)]
public record ProcessAccountDeletionCommand(Guid RequestId) : IRequest;
