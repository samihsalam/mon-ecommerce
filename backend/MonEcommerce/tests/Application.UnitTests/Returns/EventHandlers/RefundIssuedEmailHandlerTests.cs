using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.EventHandlers;

public class RefundIssuedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<RefundIssuedEmailHandler>> _logger = null!;
    private RefundIssuedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<RefundIssuedEmailHandler>>();
        _handler = new RefundIssuedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendRefundConfirmationEmailWhenRefundIssued()
    {
        var orderId = Guid.NewGuid();
        var notification = new RefundIssuedEvent(Guid.NewGuid(), orderId, "client@example.com", 15000, "#ABCD1234");

        await _handler.Handle(notification, CancellationToken.None);

        // AC #4: amount refunded, original order number, and expected processing time (3–5
        // business days) — not the raw OrderId Guid.
        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("#ABCD1234") && body.Contains("150,00") && body.Contains("3 à 5 jours ouvrés")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new RefundIssuedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", 15000, "#ABCD1234");

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
