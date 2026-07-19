using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

public class AuthServiceLoginTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<IJwtService> _jwtService = null!;
    private Mock<IApplicationDbContext> _context = null!;
    private Mock<IPublisher> _publisher = null!;
    private AuthService _authService = null!;

    [SetUp]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _jwtService = new Mock<IJwtService>();
        _context = new Mock<IApplicationDbContext>();
        _publisher = new Mock<IPublisher>();
        _authService = new AuthService(_userManager.Object, _jwtService.Object, _context.Object, _publisher.Object);
    }

    [Test]
    public async Task ShouldFailWithMessageWhenEmailIsUnknown()
    {
        _userManager
            .Setup(m => m.FindByEmailAsync("unknown@example.com"))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _authService.LoginAsync("unknown@example.com", "password123");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Email ou mot de passe incorrect."));
    }

    [Test]
    public async Task ShouldFailWithMessageWhenPasswordIsWrong()
    {
        var user = new ApplicationUser { Email = "alice@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("alice@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "wrong-password")).ReturnsAsync(false);

        var result = await _authService.LoginAsync("alice@example.com", "wrong-password");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Email ou mot de passe incorrect."));
    }

    [Test]
    public async Task ShouldSucceedAndIssueTokensWhenCredentialsAreValid()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("alice@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.CheckPasswordAsync(user, "password123")).ReturnsAsync(true);
        _userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        _jwtService.Setup(j => j.GenerateAccessToken("user-1", "alice@example.com", It.IsAny<IList<string>>())).Returns("access-token");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns("refresh-token");
        _context.Setup(c => c.RefreshTokens).Returns(Mock.Of<DbSet<RefreshToken>>());
        _context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _authService.LoginAsync("alice@example.com", "password123");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Value!.AccessToken, Is.EqualTo("access-token"));
        Assert.That(result.Value!.RefreshToken, Is.EqualTo("refresh-token"));
    }
}
