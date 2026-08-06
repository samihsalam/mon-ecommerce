using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Application.Account.EventHandlers;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Events;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.EventHandlers;

public class AccountDeletionRequestedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<AccountDeletionRequestedEmailHandler>> _logger = null!;
    private AccountDeletionRequestedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<AccountDeletionRequestedEmailHandler>>();
        _handler = new AccountDeletionRequestedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendAConfirmationEmailToTheCustomer()
    {
        var notification = new AccountDeletionRequestedEvent(Guid.NewGuid(), "alice@example.com");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "alice@example.com",
            It.IsAny<string>(),
            It.IsAny<string>(),
            "AccountDeletionRequested",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new AccountDeletionRequestedEvent(Guid.NewGuid(), "alice@example.com");

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
