using MediatR;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService) => _authService = authService;

    public Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        => _authService.ForgotPasswordAsync(request.Email, cancellationToken);
}
