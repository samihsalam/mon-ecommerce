using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.EventHandlers;

public class OrderPlacedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<OrderPlacedEmailHandler>> _logger = null!;
    private OrderPlacedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<OrderPlacedEmailHandler>>();
        _handler = new OrderPlacedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendConfirmationEmailWhenOrderPlaced()
    {
        var orderId = Guid.NewGuid();
        var notification = new OrderPlacedEvent(orderId, "user-1", "client@example.com", 28500);

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains(orderId.ToString()) && body.Contains("285,00")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new OrderPlacedEvent(Guid.NewGuid(), "user-1", "client@example.com", 28500);

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
