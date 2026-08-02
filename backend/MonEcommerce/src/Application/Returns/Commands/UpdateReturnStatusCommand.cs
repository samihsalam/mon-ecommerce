using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Commands;

// Reason (Story 7.3, AC #4): required when rejecting, emailed to the customer — deliberately not
// persisted anywhere (no Return.RejectionReason column); see Story 7.3's Dev Notes.
[Authorize(Roles = Roles.Administrator)]
public record UpdateReturnStatusCommand(Guid ReturnId, ReturnStatus NewStatus, string? Reason = null) : IRequest;
