using MediatR;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Auth.Commands;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAuthService _authService;

    public LogoutCommandHandler(IAuthService authService) => _authService = authService;

    public Task Handle(LogoutCommand request, CancellationToken cancellationToken)
        => _authService.LogoutAsync(request.RefreshToken, cancellationToken);
}
