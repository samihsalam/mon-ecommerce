using MonEcommerce.Application.Auth.EventHandlers;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.EventHandlers;

public class UserRegisteredWelcomeEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<UserRegisteredWelcomeEmailHandler>> _logger = null!;
    private UserRegisteredWelcomeEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<UserRegisteredWelcomeEmailHandler>>();
        _handler = new UserRegisteredWelcomeEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendWelcomeEmailWhenUserRegistered()
    {
        var notification = new UserRegisteredEvent("user-1", "Alice", "alice@example.com");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "alice@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("Alice")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new UserRegisteredEvent("user-1", "Alice", "alice@example.com");

        Assert.DoesNotThrowAsync(async () => await _handler.Handle(notification, CancellationToken.None));

        _logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
