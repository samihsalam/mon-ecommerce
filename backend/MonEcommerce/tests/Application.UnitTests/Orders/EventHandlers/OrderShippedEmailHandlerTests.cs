using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.EventHandlers;

public class OrderShippedEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<OrderShippedEmailHandler>> _logger = null!;
    private OrderShippedEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<OrderShippedEmailHandler>>();
        _handler = new OrderShippedEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendShipmentEmailWhenOrderShipped()
    {
        var notification = new OrderShippedEvent(Guid.NewGuid(), "client@example.com", "TRACK123");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("TRACK123")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new OrderShippedEvent(Guid.NewGuid(), "client@example.com", "TRACK123");

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
