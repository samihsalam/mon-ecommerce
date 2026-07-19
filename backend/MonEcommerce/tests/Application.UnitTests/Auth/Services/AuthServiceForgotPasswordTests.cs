using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

public class AuthServiceForgotPasswordTests
{
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private Mock<IJwtService> _jwtService = null!;
    private Mock<IApplicationDbContext> _context = null!;
    private Mock<IPublisher> _publisher = null!;
    private Mock<IConfiguration> _configuration = null!;
    private AuthService _authService = null!;

    [SetUp]
    public void Setup()
    {
        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _jwtService = new Mock<IJwtService>();
        _context = new Mock<IApplicationDbContext>();
        _publisher = new Mock<IPublisher>();
        _configuration = new Mock<IConfiguration>();
        _configuration.Setup(c => c["Frontend:BaseUrl"]).Returns("http://localhost:4200");
        _authService = new AuthService(_userManager.Object, _jwtService.Object, _context.Object, _publisher.Object, _configuration.Object);
    }

    [Test]
    public async Task ShouldSucceedWithoutPublishingWhenEmailIsUnknown()
    {
        _userManager.Setup(m => m.FindByEmailAsync("unknown@example.com")).ReturnsAsync((ApplicationUser?)null);

        var result = await _authService.ForgotPasswordAsync("unknown@example.com");

        Assert.That(result.Succeeded, Is.True);
        _publisher.Verify(p => p.Publish(It.IsAny<PasswordResetRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ShouldSucceedAndPublishAResetLinkWhenEmailIsKnown()
    {
        var user = new ApplicationUser { Id = "user-1", Email = "alice@example.com", Name = "Alice" };
        _userManager.Setup(m => m.FindByEmailAsync("alice@example.com")).ReturnsAsync(user);
        _userManager.Setup(m => m.GeneratePasswordResetTokenAsync(user)).ReturnsAsync("raw-token");

        PasswordResetRequestedEvent? published = null;
        _publisher
            .Setup(p => p.Publish(It.IsAny<PasswordResetRequestedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((evt, _) => published = (PasswordResetRequestedEvent)evt)
            .Returns(Task.CompletedTask);

        var result = await _authService.ForgotPasswordAsync("alice@example.com");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(published, Is.Not.Null);
        Assert.That(published!.ResetLink, Does.Contain("http://localhost:4200/reinitialiser-mot-de-passe"));
        Assert.That(published.ResetLink, Does.Contain("token=raw-token"));
    }
}
