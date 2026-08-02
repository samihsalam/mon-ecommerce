using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Catalogue.Queries;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Queries;

public class GetProductByIdQueryHandlerTests
{
    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public void SetNow(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private ManualTimeProvider _timeProvider = null!;
    private GetProductByIdQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();
        _timeProvider = new ManualTimeProvider();
        _timeProvider.SetNow(new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero));

        _handler = new GetProductByIdQueryHandler(_catalogueServiceMock.Object, _context, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private static ProductDetailDto MakeDetailDto(Guid productId) =>
        new(productId, "Sac cuir", "Description.", 15000, null, null, null, 5, true, Guid.NewGuid(), "Sacs", "sacs", []);

    [Test]
    public async Task Handle_ShouldCreateAViewCountRowOnTheFirstViewOfTheDay()
    {
        var productId = Guid.NewGuid();
        _catalogueServiceMock.Setup(s => s.GetProductByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeDetailDto(productId));

        await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None);

        var row = await _context.ProductDailyViewCounts.SingleAsync();
        Assert.That(row.ProductId, Is.EqualTo(productId));
        Assert.That(row.Date, Is.EqualTo(new DateOnly(2026, 4, 15)));
        Assert.That(row.ViewCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Handle_ShouldIncrementTheSameDayRowOnASecondViewTheSameDay()
    {
        var productId = Guid.NewGuid();
        _catalogueServiceMock.Setup(s => s.GetProductByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeDetailDto(productId));

        await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None);
        await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None);

        var row = await _context.ProductDailyViewCounts.SingleAsync();
        Assert.That(row.ViewCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_ShouldCreateASeparateRowForANewDay()
    {
        var productId = Guid.NewGuid();
        _catalogueServiceMock.Setup(s => s.GetProductByIdAsync(productId, It.IsAny<CancellationToken>())).ReturnsAsync(MakeDetailDto(productId));

        await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None);

        _timeProvider.SetNow(new DateTimeOffset(2026, 4, 16, 9, 0, 0, TimeSpan.Zero));
        await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None);

        var rows = await _context.ProductDailyViewCounts.OrderBy(v => v.Date).ToListAsync();
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].ViewCount, Is.EqualTo(1));
        Assert.That(rows[1].ViewCount, Is.EqualTo(1));
    }

    [Test]
    public void Handle_ShouldNotIncrementWhenTheProductIsNotFound()
    {
        var productId = Guid.NewGuid();
        _catalogueServiceMock.Setup(s => s.GetProductByIdAsync(productId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException(nameof(Product), productId));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _handler.Handle(new GetProductByIdQuery(productId), CancellationToken.None));

        Assert.That(_context.ProductDailyViewCounts.Any(), Is.False);
    }
}
