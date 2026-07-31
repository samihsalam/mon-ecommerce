using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.EventHandlers;

public class ReturnStatusUpdatedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<ReturnStatusUpdatedEmailHandler>> _logger = null!;
    private ReturnStatusUpdatedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<ReturnStatusUpdatedEmailHandler>>();
        _handler = new ReturnStatusUpdatedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendAnEmailIncludingTheNewStatusLabel()
    {
        var notification = new ReturnStatusUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", "Validé");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("Validé")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new ReturnStatusUpdatedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", "Refusé");

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
