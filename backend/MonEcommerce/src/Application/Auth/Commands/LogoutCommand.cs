using MediatR;

namespace MonEcommerce.Application.Auth.Commands;

public record LogoutCommand(string RefreshToken) : IRequest;
