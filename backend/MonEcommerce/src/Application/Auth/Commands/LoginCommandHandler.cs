using MediatR;
using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private readonly IAuthService _authService;

    public LoginCommandHandler(IAuthService authService) => _authService = authService;

    public Task<Result<AuthResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
        => _authService.LoginAsync(request.Email, request.Password, cancellationToken);
}
