using MediatR;
using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    private readonly IAuthService _authService;

    public RegisterCommandHandler(IAuthService authService) => _authService = authService;

    public Task<Result<AuthResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
        => _authService.RegisterAsync(request.Email, request.Password, cancellationToken);
}
