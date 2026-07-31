using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Commands;

[Authorize(Roles = Roles.Administrator)]
public record UpdateReturnStatusCommand(Guid ReturnId, ReturnStatus NewStatus) : IRequest;
