using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

public class AuthServiceRefreshTokenTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<IJwtService> _jwtService = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IPublisher> _publisher = null!;
    private AuthService _authService = null!;

    [SetUp]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _jwtService = new Mock<IJwtService>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _publisher = new Mock<IPublisher>();
        _authService = new AuthService(_userManager.Object, _jwtService.Object, _context, _publisher.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private async Task<RefreshToken> SeedTokenAsync(string token, string userId, DateTimeOffset expiresAt, DateTimeOffset? revokedAt = null)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
        };
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync(CancellationToken.None);
        return entity;
    }

    [Test]
    public async Task ShouldFailWhenTokenIsUnknown()
    {
        var result = await _authService.RefreshTokenAsync("does-not-exist");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Refresh token invalide ou expiré."));
    }

    [Test]
    public async Task ShouldFailWhenTokenIsExpired()
    {
        await SeedTokenAsync("expired-token", "user-1", DateTimeOffset.UtcNow.AddDays(-1));

        var result = await _authService.RefreshTokenAsync("expired-token");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Refresh token invalide ou expiré."));
    }

    [Test]
    public async Task ShouldFailWhenTokenIsAlreadyRevoked()
    {
        await SeedTokenAsync("revoked-token", "user-1", DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await _authService.RefreshTokenAsync("revoked-token");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Refresh token invalide ou expiré."));
    }

    [Test]
    public async Task ShouldFailWhenTokenIsBothExpiredAndRevoked()
    {
        await SeedTokenAsync("expired-and-revoked-token", "user-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(-1));

        var result = await _authService.RefreshTokenAsync("expired-and-revoked-token");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Refresh token invalide ou expiré."));
    }

    [Test]
    public async Task ShouldRotateTokenAndIssueNewOnesWhenValid()
    {
        var seeded = await SeedTokenAsync("valid-token", "user-1", DateTimeOffset.UtcNow.AddDays(7));

        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com" };
        _userManager.Setup(m => m.FindByIdAsync("user-1")).ReturnsAsync(user);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _jwtService.Setup(j => j.GenerateAccessToken("user-1", "alice@example.com", It.IsAny<IList<string>>())).Returns("new-access-token");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh-token");

        var result = await _authService.RefreshTokenAsync("valid-token");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.AccessToken, Is.EqualTo("new-access-token"));
        Assert.That(result.Value!.RefreshToken, Is.EqualTo("new-refresh-token"));

        var reloaded = await _context.RefreshTokens.FirstAsync(rt => rt.Id == seeded.Id);
        Assert.That(reloaded.IsRevoked, Is.True);
    }

    [Test]
    public async Task ShouldFailWhenUserNoLongerExists()
    {
        await SeedTokenAsync("orphaned-token", "deleted-user", DateTimeOffset.UtcNow.AddDays(7));
        _userManager.Setup(m => m.FindByIdAsync("deleted-user")).ReturnsAsync((ApplicationUser?)null);

        var result = await _authService.RefreshTokenAsync("orphaned-token");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Utilisateur introuvable."));
    }
}
