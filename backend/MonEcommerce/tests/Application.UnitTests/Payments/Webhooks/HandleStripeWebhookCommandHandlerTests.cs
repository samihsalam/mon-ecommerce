using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Payments.Models;
using MonEcommerce.Application.Payments.Webhooks;
using MonEcommerce.Domain.Common;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Data.Interceptors;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Payments.Webhooks;

public class HandleStripeWebhookCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private Mock<ICartService> _cartServiceMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    // IMediator (not just IPublisher): OrderPlacedEvent is raised via order.AddDomainEvent(...)
    // in the handler and only actually reaches a subscriber through
    // DispatchDomainEventsInterceptor.SavingChangesAsync, which depends on IMediator.Publish —
    // wiring that real interceptor into this test's DbContext (below) is what makes that
    // domain-event path observable at all; a bare in-memory context with no interceptors would
    // silently drop the event, since IMediator implements IPublisher this same mock also serves
    // as the handler's own IPublisher dependency for StockUnavailableEvent's direct Publish call.
    private Mock<IMediator> _mediatorMock = null!;
    private HandleStripeWebhookCommandHandler _handler = null!;

    private Guid _categoryId;
    private Guid _productId;

    [SetUp]
    public void Setup()
    {
        _mediatorMock = new Mock<IMediator>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(new DispatchDomainEventsInterceptor(_mediatorMock.Object))
            .Options;
        _context = new ApplicationDbContext(options);

        _paymentServiceMock = new Mock<IPaymentService>();
        _cartServiceMock = new Mock<ICartService>();
        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");

        _handler = new HandleStripeWebhookCommandHandler(
            _paymentServiceMock.Object,
            _cartServiceMock.Object,
            _context,
            _identityServiceMock.Object,
            _mediatorMock.Object,
            new Mock<ILogger<HandleStripeWebhookCommandHandler>>().Object);

        _categoryId = Guid.NewGuid();
        _productId = Guid.NewGuid();
        _context.Categories.Add(new Category { Id = _categoryId, Name = "Chaises", Slug = "chaises" });
        _context.Products.Add(new Product
        {
            Id = _productId,
            Name = "Chaise Scandinave",
            Description = "Une chaise",
            PriceInCents = 5000,
            IsPublished = true,
            CategoryId = _categoryId,
        });
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedStock(int quantity)
    {
        var stock = new Stock { Id = Guid.NewGuid(), ProductId = _productId, Quantity = quantity };
        _context.Stocks.Add(stock);
        return stock.Id;
    }

    private Guid SeedAddress(string userId = "user-1")
    {
        var address = new Address { Id = Guid.NewGuid(), UserId = userId, Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" };
        _context.Addresses.Add(address);
        return address.Id;
    }

    private static CartDto CartWith(Guid productId, int quantity) =>
        new([new CartItemDto(Guid.NewGuid(), productId, "Chaise Scandinave", null, 5000, quantity, 5000 * quantity)], 5000 * quantity);

    private void SetupWebhookEvent(string paymentIntentId, IReadOnlyDictionary<string, string>? metadata, long amountInCents, string type = "payment_intent.succeeded")
    {
        _paymentServiceMock
            .Setup(s => s.ParseWebhookEvent("raw-payload", "sig-header"))
            .Returns(new WebhookEvent(type, paymentIntentId, amountInCents, metadata ?? new Dictionary<string, string>()));
    }

    private static Dictionary<string, string> Metadata(string userId, Guid shippingAddressId, string shippingOptionId = "standard") => new()
    {
        ["userId"] = userId,
        ["shippingAddressId"] = shippingAddressId.ToString(),
        ["shippingOptionId"] = shippingOptionId,
    };

    [Test]
    public async Task Handle_ShouldCreateOrderDecrementStockPublishOrderPlacedAndClearCartWhenStockIsSufficient()
    {
        var addressId = SeedAddress();
        var stockId = SeedStock(10);
        await _context.SaveChangesAsync(CancellationToken.None);

        // 3 x 5000 (cart) + 490 (standard shipping) = 15490 — must equal what Stripe actually
        // charged, or the new cart/charge-amount consistency guard treats it as drift and refunds.
        SetupWebhookEvent("pi_ok_1", Metadata("user-1", addressId), 15490);
        _cartServiceMock.Setup(s => s.GetCartAsync(It.Is<CartOwner>(o => o.UserId == "user-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(_productId, 3));

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        var order = await _context.Orders.Include(o => o.Items).SingleAsync();
        Assert.That(order.UserId, Is.EqualTo("user-1"));
        Assert.That(order.ShippingAddressId, Is.EqualTo(addressId));
        Assert.That(order.StripePaymentIntentId, Is.EqualTo("pi_ok_1"));
        Assert.That(order.TotalInCents, Is.EqualTo(15490));
        Assert.That(order.Items, Has.Count.EqualTo(1));
        Assert.That(order.Items[0].Quantity, Is.EqualTo(3));

        var stock = await _context.Stocks.SingleAsync(s => s.Id == stockId);
        Assert.That(stock.Quantity, Is.EqualTo(7));

        var auditLog = await _context.PaymentAuditLogs.SingleAsync();
        Assert.That(auditLog.Outcome, Is.EqualTo(PaymentAuditOutcome.Confirmed));
        Assert.That(auditLog.OrderId, Is.EqualTo(order.Id));

        // DispatchDomainEventsInterceptor calls _mediator.Publish(domainEvent) where the loop
        // variable is statically typed BaseEvent, so Moq records this as a Publish<BaseEvent>
        // invocation (not Publish<OrderPlacedEvent>) regardless of the runtime type — match on
        // BaseEvent and inspect the actual instance, rather than It.Is<OrderPlacedEvent>, which
        // would target a different generic instantiation than what was actually invoked.
        _mediatorMock.Verify(
            p => p.Publish(
                It.Is<BaseEvent>(e => e is OrderPlacedEvent && ((OrderPlacedEvent)e).OrderId == order.Id && ((OrderPlacedEvent)e).CustomerEmail == "alice@example.com"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _cartServiceMock.Verify(s => s.ClearCartAsync(It.Is<CartOwner>(o => o.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
        _paymentServiceMock.Verify(s => s.CreateRefundAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ShouldRefundAndPublishStockUnavailableWithoutCreatingAnOrderWhenStockIsInsufficient()
    {
        var addressId = SeedAddress();
        SeedStock(2);
        await _context.SaveChangesAsync(CancellationToken.None);

        SetupWebhookEvent("pi_oversell_1", Metadata("user-1", addressId), 5490);
        _cartServiceMock.Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(_productId, 5)); // wants 5, only 2 in stock

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        Assert.That(await _context.Orders.AnyAsync(), Is.False);

        var stock = await _context.Stocks.SingleAsync();
        Assert.That(stock.Quantity, Is.EqualTo(2), "stock must be untouched when the order is never created");

        var auditLog = await _context.PaymentAuditLogs.SingleAsync();
        Assert.That(auditLog.Outcome, Is.EqualTo(PaymentAuditOutcome.Refunded));
        Assert.That(auditLog.OrderId, Is.Null);

        _paymentServiceMock.Verify(s => s.CreateRefundAsync("pi_oversell_1", 5490, It.IsAny<CancellationToken>()), Times.Once);
        _mediatorMock.Verify(p => p.Publish(It.Is<StockUnavailableEvent>(e => e.CustomerEmail == "alice@example.com" && e.AmountInCents == 5490), It.IsAny<CancellationToken>()), Times.Once);
        _cartServiceMock.Verify(s => s.ClearCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ShouldBeIdempotentOnADuplicateWebhookDelivery()
    {
        var addressId = SeedAddress();
        SeedStock(10);
        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            Id = Guid.NewGuid(),
            StripePaymentIntentId = "pi_already_processed",
            UserId = "user-1",
            AmountInCents = 5490,
            Outcome = PaymentAuditOutcome.Confirmed,
            OrderId = Guid.NewGuid(),
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        SetupWebhookEvent("pi_already_processed", Metadata("user-1", addressId), 5490);

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        _cartServiceMock.Verify(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()), Times.Never);
        _paymentServiceMock.Verify(s => s.CreateRefundAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(await _context.PaymentAuditLogs.CountAsync(), Is.EqualTo(1), "must not write a second audit row for the same payment intent");
    }

    [Test]
    public async Task Handle_ShouldNoOpWithoutRefundingWhenCartIsAlreadyEmpty()
    {
        // An empty cart at this point (idempotency guard already checked above) means a
        // redelivery raced just ahead of this exact payment intent's own audit-log row becoming
        // visible — not a sign stock ran out, so this must NOT trigger a refund.
        var addressId = SeedAddress();
        SeedStock(10);
        await _context.SaveChangesAsync(CancellationToken.None);

        SetupWebhookEvent("pi_empty_cart", Metadata("user-1", addressId), 5490);
        _cartServiceMock.Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartDto([], 0));

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        Assert.That(await _context.Orders.AnyAsync(), Is.False);
        Assert.That(await _context.PaymentAuditLogs.AnyAsync(), Is.False);
        _paymentServiceMock.Verify(s => s.CreateRefundAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediatorMock.Verify(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ShouldRefundInsteadOfCreatingAMismatchedOrderWhenCartDriftedAfterPayment()
    {
        // Stripe charged 5490 (the cart+shipping total at the time payment was confirmed), but by
        // the time this webhook fires the live cart has since changed (e.g. edited in another
        // tab) to a different total — must not silently create an Order for whatever the cart
        // currently holds under a stale charge.
        var addressId = SeedAddress();
        SeedStock(10);
        await _context.SaveChangesAsync(CancellationToken.None);

        SetupWebhookEvent("pi_drifted", Metadata("user-1", addressId), 5490);
        _cartServiceMock.Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(_productId, 3)); // 15000 + 490 = 15490 != the charged 5490

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        Assert.That(await _context.Orders.AnyAsync(), Is.False);

        var stock = await _context.Stocks.SingleAsync();
        Assert.That(stock.Quantity, Is.EqualTo(10), "stock must be untouched when the order is never created");

        var auditLog = await _context.PaymentAuditLogs.SingleAsync();
        Assert.That(auditLog.Outcome, Is.EqualTo(PaymentAuditOutcome.Refunded));
        _paymentServiceMock.Verify(s => s.CreateRefundAsync("pi_drifted", 5490, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase("payment_intent.payment_failed")]
    [TestCase("charge.refunded")]
    public async Task Handle_ShouldIgnoreNonSucceededEventTypes(string eventType)
    {
        SetupWebhookEvent("pi_irrelevant", null, 5490, eventType);

        await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None);

        Assert.That(await _context.Orders.AnyAsync(), Is.False);
        Assert.That(await _context.PaymentAuditLogs.AnyAsync(), Is.False);
        _cartServiceMock.Verify(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ShouldSkipSilentlyWhenMetadataIsMissingOrMalformed()
    {
        // A payment_intent.succeeded whose PaymentIntent was never created by
        // CreatePaymentIntentCommandHandler (e.g. metadata stripped, or a stale/unrelated
        // intent) — nothing safe to reconstruct an order from. Must not throw (Stripe would
        // retry forever for a payload that can never become processable).
        SetupWebhookEvent("pi_no_metadata", new Dictionary<string, string>(), 5490);

        Assert.DoesNotThrowAsync(async () =>
            await _handler.Handle(new HandleStripeWebhookCommand("raw-payload", "sig-header"), CancellationToken.None));

        Assert.That(await _context.Orders.AnyAsync(), Is.False);
        Assert.That(await _context.PaymentAuditLogs.AnyAsync(), Is.False);
    }

    // Lower-level test of the assumption HandleStripeWebhookCommandHandler's retry loop depends
    // on: that Stock.RowVersion (SQL Server's rowversion, resolving AC #1's "xmin PostgreSQL"
    // wording — see Story 4.6's Dev Notes) actually causes EF Core to throw
    // DbUpdateConcurrencyException when two separate contexts race on the same row, rather than
    // silently letting the second writer clobber the first's decrement. Exercised directly
    // against two ApplicationDbContext instances sharing one InMemory database, since reliably
    // interleaving a second writer inside the handler's own single, synchronous
    // read-decrement-save call (with no externally-controllable await point) isn't possible from
    // a sequential test.
    //
    // contextB's RowVersion is reassigned by hand before its SaveChanges — on a real SQL Server
    // rowversion column the engine regenerates that value automatically on every UPDATE; EF
    // Core's InMemory provider (used here for speed/isolation, same as every other test in this
    // file) does NOT simulate that automatic regeneration, so without this line contextA's stale
    // "original" RowVersion would still match the store's current value and no conflict would
    // ever be detected — this line stands in for what SQL Server itself would do, isolating
    // exactly the one thing this test cares about: does EF's concurrency-token comparison
    // actually throw when the two don't match.
    [Test]
    public async Task StockRowVersion_ShouldCauseAConcurrencyExceptionWhenTwoContextsRaceOnTheSameRow()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options;

        Guid stockId;
        await using (var seedContext = new ApplicationDbContext(options))
        {
            seedContext.Categories.Add(new Category { Id = _categoryId, Name = "Chaises", Slug = "chaises" });
            seedContext.Products.Add(new Product { Id = _productId, Name = "Chaise", Description = "d", PriceInCents = 5000, IsPublished = true, CategoryId = _categoryId });
            var stock = new Stock { Id = Guid.NewGuid(), ProductId = _productId, Quantity = 5 };
            seedContext.Stocks.Add(stock);
            await seedContext.SaveChangesAsync(CancellationToken.None);
            stockId = stock.Id;
        }

        await using var contextA = new ApplicationDbContext(options);
        await using var contextB = new ApplicationDbContext(options);

        var stockAsSeenByA = await contextA.Stocks.SingleAsync(s => s.Id == stockId);
        var stockAsSeenByB = await contextB.Stocks.SingleAsync(s => s.Id == stockId);

        stockAsSeenByB.Quantity -= 2;
        stockAsSeenByB.RowVersion = Guid.NewGuid().ToByteArray(); // simulate SQL Server's automatic rowversion bump
        await contextB.SaveChangesAsync(CancellationToken.None); // wins the race

        stockAsSeenByA.Quantity -= 1; // still holds A's now-stale original RowVersion
        Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await contextA.SaveChangesAsync(CancellationToken.None));
    }
}
