using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Common.Security;

namespace MonEcommerce.Application.Account.Commands;

[Authorize]
public record UpdateProfileCommand(string Name, string Email, string? CurrentPassword) : IRequest<Result<ProfileDto>>;
