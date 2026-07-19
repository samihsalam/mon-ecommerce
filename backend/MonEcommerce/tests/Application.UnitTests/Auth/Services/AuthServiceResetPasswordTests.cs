using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

// Deliberately uses a REAL UserManager + token provider (not a mock) — the thing under test
// here is Identity's own token validation and password policy. Mocking UserManager would
// hide exactly the class of bug this story found (see Dev Notes in the story file): the
// missing AddDefaultTokenProviders() and the unconfigured, overly strict default password
// policy were both invisible to every prior test in this codebase because they all mock
// UserManager.
public class AuthServiceResetPasswordTests
{
    private ApplicationDbContext _context = null!;
    private ServiceProvider _provider = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private Mock<IJwtService> _jwtService = null!;
    private Mock<IPublisher> _publisher = null!;
    private Mock<IConfiguration> _configuration = null!;
    private AuthService _authService = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(_context);
        services.AddLogging();
        services.AddDataProtection();
        services
            .AddIdentityCore<ApplicationUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        _provider = services.BuildServiceProvider();
        _userManager = _provider.GetRequiredService<UserManager<ApplicationUser>>();

        _jwtService = new Mock<IJwtService>();
        _publisher = new Mock<IPublisher>();
        _configuration = new Mock<IConfiguration>();
        _authService = new AuthService(_userManager, _jwtService.Object, _context, _publisher.Object, _configuration.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _userManager.Dispose();
        _context.Dispose();
        _provider.Dispose();
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string password)
    {
        var user = new ApplicationUser { UserName = email, Email = email, Name = "Alice" };
        var result = await _userManager.CreateAsync(user, password);
        Assert.That(result.Succeeded, Is.True, string.Join(", ", result.Errors.Select(e => e.Description)));
        return user;
    }

    [Test]
    public async Task ShouldFailWithGenericMessageWhenEmailIsUnknown()
    {
        var result = await _authService.ResetPasswordAsync("unknown@example.com", "any-token", "newpassword123");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Ce lien de réinitialisation est invalide ou a expiré."));
    }

    [Test]
    public async Task ShouldFailWithGenericMessageWhenTokenIsInvalid()
    {
        await CreateUserAsync("alice@example.com", "originalpassword1");

        var result = await _authService.ResetPasswordAsync("alice@example.com", "not-a-real-token", "newpassword123");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Errors, Does.Contain("Ce lien de réinitialisation est invalide ou a expiré."));
    }

    [Test]
    public async Task ShouldSucceedRevokeRefreshTokensAndMakeTheTokenSingleUseWhenValid()
    {
        var user = await CreateUserAsync("alice@example.com", "originalpassword1");
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = "still-active-refresh-token",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(6),
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _authService.ResetPasswordAsync("alice@example.com", token, "brandnewpassword1");

        Assert.That(result.Succeeded, Is.True);

        var passwordCheck = await _userManager.CheckPasswordAsync(user, "brandnewpassword1");
        Assert.That(passwordCheck, Is.True);

        var refreshToken = await _context.RefreshTokens.FirstAsync(rt => rt.UserId == user.Id);
        Assert.That(refreshToken.IsRevoked, Is.True);

        // Single-use: the same token must fail on a second attempt, because ResetPasswordAsync
        // already rotated the user's SecurityStamp on the first (successful) use.
        var secondAttempt = await _authService.ResetPasswordAsync("alice@example.com", token, "yetanotherpassword1");
        Assert.That(secondAttempt.Succeeded, Is.False);
    }
}
