using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}
