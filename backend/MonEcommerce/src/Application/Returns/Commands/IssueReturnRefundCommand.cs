using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Returns.Commands;

// No amount parameter — AC #2 refunds "the original payment amount", not an admin-chosen one,
// same "server is the source of truth for money" principle as Story 4.5/4.6.
[Authorize(Roles = Roles.Administrator)]
public record IssueReturnRefundCommand(Guid ReturnId) : IRequest;
