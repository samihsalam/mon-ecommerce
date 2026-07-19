using MediatR;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result>;
