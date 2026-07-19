using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

public class AuthServiceLogoutTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<IJwtService> _jwtService = null!;
    private ApplicationDbContext _context = null!;
    private Mock<IPublisher> _publisher = null!;
    private Mock<IConfiguration> _configuration = null!;
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
        _configuration = new Mock<IConfiguration>();
        _authService = new AuthService(_userManager.Object, _jwtService.Object, _context, _publisher.Object, _configuration.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task ShouldRevokeAnUnrevokedToken()
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "active-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
        };
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _authService.LogoutAsync("active-token");

        var reloaded = await _context.RefreshTokens.FirstAsync(rt => rt.Id == entity.Id);
        Assert.That(reloaded.IsRevoked, Is.True);
    }

    [Test]
    public void ShouldNotThrowWhenTokenIsUnknown()
    {
        Assert.DoesNotThrowAsync(async () => await _authService.LogoutAsync("does-not-exist"));
    }

    [Test]
    public async Task ShouldNotThrowAndLeaveRevokedAtUnchangedWhenTokenIsAlreadyRevoked()
    {
        var revokedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "already-revoked-token",
            UserId = "user-1",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
            RevokedAt = revokedAt,
        };
        _context.RefreshTokens.Add(entity);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _authService.LogoutAsync("already-revoked-token");

        var reloaded = await _context.RefreshTokens.FirstAsync(rt => rt.Id == entity.Id);
        Assert.That(reloaded.RevokedAt, Is.EqualTo(revokedAt));
    }
}
