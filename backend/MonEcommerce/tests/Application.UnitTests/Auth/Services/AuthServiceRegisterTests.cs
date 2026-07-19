using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Services;

public class AuthServiceRegisterTests
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
        _authService = new AuthService(_userManager.Object, _jwtService.Object, _context.Object, _publisher.Object, _configuration.Object);
    }

    [Test]
    public void ShouldThrowConflictExceptionWhenEmailAlreadyRegistered()
    {
        _userManager
            .Setup(m => m.FindByEmailAsync("alice@example.com"))
            .ReturnsAsync(new ApplicationUser { Email = "alice@example.com" });

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _authService.RegisterAsync("Alice", "alice@example.com", "password123"));

        _userManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void ShouldThrowConflictExceptionWhenCreateAsyncRacesIntoADuplicateEmail()
    {
        _userManager
            .Setup(m => m.FindByEmailAsync("alice@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ThrowsAsync(new DbUpdateException("unique index violation"));

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _authService.RegisterAsync("Alice", "alice@example.com", "password123"));
    }
}
