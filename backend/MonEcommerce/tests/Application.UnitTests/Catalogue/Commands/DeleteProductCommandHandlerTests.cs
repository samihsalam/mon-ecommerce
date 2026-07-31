using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class DeleteProductCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private DeleteProductCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new DeleteProductCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedProduct(bool isDeleted = false)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
            IsPublished = true,
            IsDeleted = isDeleted,
        };
        _context.Products.Add(product);
        return product.Id;
    }

    [Test]
    public async Task Handle_ShouldSoftDeleteAndInvalidateTheCatalogueCache()
    {
        var productId = SeedProduct();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DeleteProductCommand(productId), CancellationToken.None);

        var product = await _context.Products.SingleAsync(p => p.Id == productId);
        // AC #3: soft-deleted, not physically removed — the row still exists.
        Assert.That(product.IsDeleted, Is.True);

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldBeIdempotentWhenTheProductIsAlreadyDeleted()
    {
        var productId = SeedProduct(isDeleted: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DeleteProductCommand(productId), CancellationToken.None);

        var product = await _context.Products.SingleAsync(p => p.Id == productId);
        Assert.That(product.IsDeleted, Is.True);

        // Already deleted — no-op, so no redundant cache invalidation either.
        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new DeleteProductCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
