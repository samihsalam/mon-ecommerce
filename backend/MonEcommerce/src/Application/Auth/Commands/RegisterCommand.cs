using MediatR;
using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public record RegisterCommand(string Name, string Email, string Password) : IRequest<Result<AuthResponse>>;
