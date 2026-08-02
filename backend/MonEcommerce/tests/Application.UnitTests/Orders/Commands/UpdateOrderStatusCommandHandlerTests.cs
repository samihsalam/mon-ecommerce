using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.Commands;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.Commands;

public class UpdateOrderStatusCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private Mock<IConfiguration> _configurationMock = null!;
    private UpdateOrderStatusCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");

        _configurationMock = new Mock<IConfiguration>();
        _configurationMock.Setup(c => c["Frontend:BaseUrl"]).Returns("https://example.com/");

        _handler = new UpdateOrderStatusCommandHandler(_context, _identityServiceMock.Object, _configurationMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedOrder(OrderStatus status = OrderStatus.Pending)
    {
        var address = new Address { Id = Guid.NewGuid(), UserId = "user-1", Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" };
        _context.Addresses.Add(address);
        var order = new Order { Id = Guid.NewGuid(), UserId = "user-1", Status = status, TotalInCents = 5000, ShippingAddressId = address.Id };
        _context.Orders.Add(order);
        return order.Id;
    }

    [Test]
    public async Task Handle_ShouldSetStatusAndTrackingNumberAndPublishOrderShippedWhenMovingToShipped()
    {
        // Processing -> Shipped is the valid next stage (Pending -> Shipped would skip a stage
        // and is rejected since Story 7.2's transition rules).
        var orderId = SeedOrder(OrderStatus.Processing);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Shipped, "TRACK123"), CancellationToken.None);

        var order = await _context.Orders.SingleAsync();
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Shipped));
        Assert.That(order.TrackingNumber, Is.EqualTo("TRACK123"));

        var domainEvent = order.DomainEvents.OfType<OrderShippedEvent>().Single();
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
        Assert.That(domainEvent.TrackingNumber, Is.EqualTo("TRACK123"));
        Assert.That(domainEvent.TrackingLink, Is.EqualTo($"https://example.com/compte/commandes/{orderId}"));
    }

    [Test]
    public async Task Handle_ShouldPublishOrderDeliveredWhenMovingToDelivered()
    {
        var orderId = SeedOrder(OrderStatus.Shipped);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Delivered, null), CancellationToken.None);

        var order = await _context.Orders.SingleAsync();
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Delivered));

        var domainEvent = order.DomainEvents.OfType<OrderDeliveredEvent>().Single();
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
    }

    [Test]
    public async Task Handle_ShouldPublishNoEventForOtherTransitions()
    {
        var orderId = SeedOrder(OrderStatus.Pending);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Processing, null), CancellationToken.None);

        var order = await _context.Orders.SingleAsync();
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
        Assert.That(order.DomainEvents, Is.Empty);
    }

    // AC #4: every status change is logged (previous status, new status, admin ID, timestamp).
    [Test]
    public async Task Handle_ShouldWriteOneOrderStatusHistoryRowPerSuccessfulChange()
    {
        var orderId = SeedOrder(OrderStatus.Pending);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Processing, null), CancellationToken.None);

        var history = await _context.OrderStatusHistories.SingleAsync();
        Assert.That(history.OrderId, Is.EqualTo(orderId));
        Assert.That(history.PreviousStatus, Is.EqualTo(OrderStatus.Pending));
        Assert.That(history.NewStatus, Is.EqualTo(OrderStatus.Processing));
    }

    [TestCase(OrderStatus.Pending, OrderStatus.Processing)]
    [TestCase(OrderStatus.Pending, OrderStatus.Cancelled)]
    [TestCase(OrderStatus.Processing, OrderStatus.Shipped)]
    [TestCase(OrderStatus.Processing, OrderStatus.Cancelled)]
    [TestCase(OrderStatus.Shipped, OrderStatus.Delivered)]
    public async Task Handle_ShouldAllowEveryValidTransition(OrderStatus from, OrderStatus to)
    {
        var orderId = SeedOrder(from);
        await _context.SaveChangesAsync(CancellationToken.None);

        var trackingNumber = to == OrderStatus.Shipped ? "TRACK1" : null;
        await _handler.Handle(new UpdateOrderStatusCommand(orderId, to, trackingNumber), CancellationToken.None);

        var order = await _context.Orders.SingleAsync();
        Assert.That(order.Status, Is.EqualTo(to));
    }

    // AC #3: e.g. "Livrée" -> "En préparation" — an invalid (backward) transition.
    [TestCase(OrderStatus.Delivered, OrderStatus.Processing)]
    // Skipping ahead.
    [TestCase(OrderStatus.Pending, OrderStatus.Shipped)]
    // Staying at the same status.
    [TestCase(OrderStatus.Processing, OrderStatus.Processing)]
    // AC #6: cancellation is only possible from Pending/Processing — not from Shipped.
    [TestCase(OrderStatus.Shipped, OrderStatus.Cancelled)]
    // Terminal states.
    [TestCase(OrderStatus.Delivered, OrderStatus.Cancelled)]
    [TestCase(OrderStatus.Cancelled, OrderStatus.Processing)]
    public void Handle_ShouldRejectInvalidTransitionsWithTheValidTransitionsList(OrderStatus from, OrderStatus to)
    {
        var orderId = SeedOrder(from);
        _context.SaveChanges();

        var ex = Assert.ThrowsAsync<MonEcommerce.Application.Common.Exceptions.ValidationException>(async () =>
            await _handler.Handle(new UpdateOrderStatusCommand(orderId, to, "TRACK1"), CancellationToken.None));

        Assert.That(ex!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task Handle_ShouldNotWriteAnOrderStatusHistoryRowOrMutateTheOrderWhenTheTransitionIsRejected()
    {
        var orderId = SeedOrder(OrderStatus.Delivered);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<MonEcommerce.Application.Common.Exceptions.ValidationException>(async () =>
            await _handler.Handle(new UpdateOrderStatusCommand(orderId, OrderStatus.Cancelled, null), CancellationToken.None));

        Assert.That(await _context.OrderStatusHistories.CountAsync(), Is.EqualTo(0));
        var order = await _context.Orders.SingleAsync();
        Assert.That(order.Status, Is.EqualTo(OrderStatus.Delivered));
    }
}
