using MonEcommerce.Application.Common.Security;

namespace MonEcommerce.Application.Account.Commands;

// No parameters — resolved via IUser inside the handler, same convention as
// CreateReturnRequestCommand: a deletion request is only ever made by an authenticated customer
// about their own account.
[Authorize]
public record RequestAccountDeletionCommand : IRequest<Guid>;
