using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.EventHandlers;

public class ReturnRequestedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<ReturnRequestedEmailHandler>> _logger = null!;
    private ReturnRequestedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<ReturnRequestedEmailHandler>>();
        _handler = new ReturnRequestedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendAcknowledgementEmailWhenReturnRequested()
    {
        var orderId = Guid.NewGuid();
        var notification = new ReturnRequestedEvent(Guid.NewGuid(), orderId, "client@example.com", "Produit non conforme");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains(orderId.ToString()) && body.Contains("Produit non conforme")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ShouldHtmlEncodeReasonToPreventInjection()
    {
        var notification = new ReturnRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", "<script>alert(1)</script>");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => !body.Contains("<script>")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new ReturnRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), "client@example.com", "Produit défectueux");

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
