using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.EventHandlers;
using MonEcommerce.Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.EventHandlers;

public class OrderDeliveredEmailHandlerTests
{
    private Mock<IEmailService> _emailService = null!;
    private Mock<ILogger<OrderDeliveredEmailHandler>> _logger = null!;
    private OrderDeliveredEmailHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _emailService = new Mock<IEmailService>();
        _logger = new Mock<ILogger<OrderDeliveredEmailHandler>>();
        _handler = new OrderDeliveredEmailHandler(_emailService.Object, _logger.Object);
    }

    [Test]
    public async Task ShouldSendDeliveryEmailWhenOrderDelivered()
    {
        var orderId = Guid.NewGuid();
        var notification = new OrderDeliveredEvent(orderId, "client@example.com");

        await _handler.Handle(notification, CancellationToken.None);

        _emailService.Verify(e => e.SendAsync(
            "client@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains(orderId.ToString())),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void ShouldLogErrorAndNotThrowWhenEmailServiceFails()
    {
        _emailService
            .Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SendGrid unavailable"));

        var notification = new OrderDeliveredEvent(Guid.NewGuid(), "client@example.com");

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
