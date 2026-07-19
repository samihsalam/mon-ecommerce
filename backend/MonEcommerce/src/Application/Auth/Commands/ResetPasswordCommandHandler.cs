using MediatR;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Auth.Commands;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ResetPasswordCommandHandler(IAuthService authService) => _authService = authService;

    public Task<Result> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        => _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword, cancellationToken);
}
