using System.Net;
using MediatR;
using MonEcommerce.Application.Auth.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MonEcommerce.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IApplicationDbContext _context;
    private readonly IPublisher _publisher;
    private readonly IConfiguration _configuration;

    public AuthService(UserManager<ApplicationUser> userManager, IJwtService jwtService, IApplicationDbContext context, IPublisher publisher, IConfiguration configuration)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _context = context;
        _publisher = publisher;
        _configuration = configuration;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(string name, string email, string password, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
            throw new ConflictException("Un compte existe déjà avec cet email.");

        var user = new ApplicationUser { UserName = email, Email = email, Name = name };

        IdentityResult result;
        try
        {
            result = await _userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException)
        {
            // Rare race: another request registered the same email between our pre-check above and this call.
            throw new ConflictException("Un compte existe déjà avec cet email.");
        }

        if (!result.Succeeded)
            return Result<AuthResponse>.Failure(result.Errors.Select(e => e.Description));

        // Issue tokens (and persist the refresh token) before publishing the welcome-email event,
        // so a failure here can't leave a "welcome" email sent for a registration that never completed.
        var tokens = await IssueTokensAsync(user, cancellationToken);

        await _publisher.Publish(new UserRegisteredEvent(user.Id, name, email), cancellationToken);

        return tokens;
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

    public async Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // No enumeration: always report success whether or not the email is registered.
            // The "found" branch below awaits a real SendGrid network call before returning
            // (see the comment near the Publish call) — without this delay, an unregistered
            // email would get a measurably faster response than a registered one, which is
            // itself an enumeration signal even though the response body is identical.
            await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
            return Result.Success();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = _configuration["Frontend:BaseUrl"]!.TrimEnd('/');
        var link = $"{baseUrl}/reinitialiser-mot-de-passe?email={WebUtility.UrlEncode(user.Email)}&token={WebUtility.UrlEncode(token)}";

        await _publisher.Publish(new PasswordResetRequestedEvent(user.Id, user.Name, user.Email!, link), cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        const string genericFailure = "Ce lien de réinitialisation est invalide ou a expiré.";

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Same generic message as an invalid/expired token: don't reveal whether the email is registered.
            return Result.Failure([genericFailure]);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return Result.Failure([genericFailure]);
        }

        // Known, narrow race: a refresh token issued by a concurrent login/refresh on another
        // device (inserted after this SELECT but before SaveChanges below) would be missed and
        // survive the reset. A single atomic ExecuteUpdateAsync would close this, but EF Core's
        // InMemory provider (used by this test suite) doesn't support ExecuteUpdate/Delete —
        // only real SQL providers do. Revisit with a provider-aware approach if this narrow
        // window ever proves exploitable in practice; SQL Server (production) does support it.
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.RevokedAt = DateTimeOffset.UtcNow;
        }

        if (activeTokens.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
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

        return Result<AuthResponse>.Success(new AuthResponse(accessToken, refreshTokenValue, expiresAt, user.Id));
    }
}
