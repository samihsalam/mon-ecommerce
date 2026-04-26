using MediatR;
using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;
