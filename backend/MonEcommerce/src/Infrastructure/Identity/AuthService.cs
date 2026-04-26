using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MonEcommerce.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IApplicationDbContext _context;

    public AuthService(UserManager<ApplicationUser> userManager, IJwtService jwtService, IApplicationDbContext context)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _context = context;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await _userManager.CreateAsync(user, password);

        if (!result.Succeeded)
            return Result<AuthResponse>.Failure(result.Errors.Select(e => e.Description));

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<Result<AuthResponse>> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, password))
            return Result<AuthResponse>.Failure(["Email ou mot de passe incorrect."]);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var existing = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (existing == null || existing.IsRevoked || existing.ExpiresAt <= DateTimeOffset.UtcNow)
            return Result<AuthResponse>.Failure(["Refresh token invalide ou expiré."]);

        var user = await _userManager.FindByIdAsync(existing.UserId);
        if (user == null)
            return Result<AuthResponse>.Failure(["Utilisateur introuvable."]);

        existing.RevokedAt = DateTimeOffset.UtcNow;

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (token != null && !token.IsRevoked)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Result<AuthResponse>> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email!, roles);
        var refreshTokenValue = _jwtService.GenerateRefreshToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshTokenValue,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        });

        await _context.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshTokenValue, expiresAt));
    }
}
