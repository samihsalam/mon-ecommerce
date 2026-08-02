using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Queries;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Queries;

public class GetStockHistoryQueryHandlerTests
{
    private ApplicationDbContext _context = null!;
    private GetStockHistoryQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _handler = new GetStockHistoryQueryHandler(_context);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task Handle_ShouldReturnMovementsForTheProductOrderedByMostRecentFirst()
    {
        var productId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();

        _context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), ProductId = productId, PreviousQuantity = 0, NewQuantity = 10, Reason = "Réception initiale", Created = DateTimeOffset.UtcNow.AddDays(-2) });
        _context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), ProductId = productId, PreviousQuantity = 10, NewQuantity = 4, Reason = "Vente", Created = DateTimeOffset.UtcNow.AddDays(-1) });
        _context.StockMovements.Add(new StockMovement { Id = Guid.NewGuid(), ProductId = otherProductId, PreviousQuantity = 5, NewQuantity = 3, Reason = "Vente", Created = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetStockHistoryQuery(productId), CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result[0].Reason, Is.EqualTo("Vente"));
        Assert.That(result[1].Reason, Is.EqualTo("Réception initiale"));
    }

    [Test]
    public async Task Handle_ShouldReturnEmptyListForAProductWithNoHistory()
    {
        var result = await _handler.Handle(new GetStockHistoryQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.That(result, Is.Empty);
    }
}
