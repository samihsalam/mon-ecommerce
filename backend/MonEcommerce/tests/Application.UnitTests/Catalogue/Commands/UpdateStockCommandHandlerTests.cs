using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class UpdateStockCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private UpdateStockCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new UpdateStockCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedProduct(int quantity = 20, int alertThreshold = 5)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
            Stock = new Stock { Id = Guid.NewGuid(), Quantity = quantity, AlertThreshold = alertThreshold },
        };
        _context.Products.Add(product);
        return product.Id;
    }

    [Test]
    public async Task Handle_ShouldUpdateQuantityAndThresholdAndLogAMovement()
    {
        var productId = SeedProduct(quantity: 20, alertThreshold: 5);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new UpdateStockCommand(productId, 30, 8, "Réapprovisionnement"), CancellationToken.None);

        Assert.That(result.Quantity, Is.EqualTo(30));
        Assert.That(result.AlertThreshold, Is.EqualTo(8));

        var stock = await _context.Stocks.SingleAsync(s => s.ProductId == productId);
        Assert.That(stock.Quantity, Is.EqualTo(30));
        Assert.That(stock.AlertThreshold, Is.EqualTo(8));

        var movement = await _context.StockMovements.SingleAsync();
        Assert.That(movement.PreviousQuantity, Is.EqualTo(20));
        Assert.That(movement.NewQuantity, Is.EqualTo(30));
        Assert.That(movement.Reason, Is.EqualTo("Réapprovisionnement"));

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldDefaultReasonWhenNoneProvided()
    {
        var productId = SeedProduct();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateStockCommand(productId, 10, 5, null), CancellationToken.None);

        var movement = await _context.StockMovements.SingleAsync();
        Assert.That(movement.Reason, Is.EqualTo("Ajustement manuel"));
    }

    [Test]
    public async Task Handle_ShouldPublishStockUpdatedEventWhenAtOrBelowThreshold()
    {
        var productId = SeedProduct(quantity: 20, alertThreshold: 5);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateStockCommand(productId, 5, 5, null), CancellationToken.None);

        // Same DbContext instance as the handler used (no interceptor wired in this bare test
        // context, so domain events are never auto-dispatched/cleared) — EF Core's identity map
        // returns the same tracked Stock instance the handler attached the event to.
        var stock = await _context.Stocks.SingleAsync(s => s.ProductId == productId);
        var domainEvent = stock.DomainEvents.OfType<StockUpdatedEvent>().SingleOrDefault();
        Assert.That(domainEvent, Is.Not.Null);
        Assert.That(domainEvent!.NewQuantity, Is.EqualTo(5));
        Assert.That(domainEvent.AlertThreshold, Is.EqualTo(5));
    }

    [Test]
    public async Task Handle_ShouldNotPublishStockUpdatedEventWhenAboveThreshold()
    {
        var productId = SeedProduct(quantity: 2, alertThreshold: 5);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateStockCommand(productId, 50, 5, null), CancellationToken.None);

        var stock = await _context.Stocks.SingleAsync(s => s.ProductId == productId);
        Assert.That(stock.DomainEvents.OfType<StockUpdatedEvent>(), Is.Empty);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new UpdateStockCommand(Guid.NewGuid(), 10, 5, null), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundForASoftDeletedProduct()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
            IsDeleted = true,
            Stock = new Stock { Id = Guid.NewGuid(), Quantity = 10, AlertThreshold = 5 },
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new UpdateStockCommand(product.Id, 10, 5, null), CancellationToken.None));
    }
}
