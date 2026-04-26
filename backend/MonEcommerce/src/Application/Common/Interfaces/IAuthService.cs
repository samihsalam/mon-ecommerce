using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
}
