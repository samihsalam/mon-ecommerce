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

public class UpdateProductCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private UpdateProductCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new UpdateProductCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private (Guid ProductId, Guid CategoryId) SeedProduct(bool isDeleted = false)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Sacs", Slug = "sacs" };
        _context.Categories.Add(category);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = category.Id,
            IsPublished = true,
            IsDeleted = isDeleted,
            Stock = new Stock { Id = Guid.NewGuid(), Quantity = 5 },
        };
        _context.Products.Add(product);

        return (product.Id, category.Id);
    }

    [Test]
    public async Task Handle_ShouldUpdateFieldsAndInvalidateTheCatalogueCache()
    {
        var (productId, categoryId) = SeedProduct();
        await _context.SaveChangesAsync(CancellationToken.None);

        var command = new UpdateProductCommand(productId, "Sac cuir premium", "Description mise à jour.", 18000, categoryId, "Cuir pleine fleur", "Noir", "32x22x12cm");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.That(result.Name, Is.EqualTo("Sac cuir premium"));
        Assert.That(result.PriceInCents, Is.EqualTo(18000));
        // Untouched by this command — IsPublished belongs to Story 6.5's own endpoint.
        Assert.That(result.IsPublished, Is.True);
        Assert.That(result.StockQuantity, Is.EqualTo(5));

        var product = await _context.Products.SingleAsync(p => p.Id == productId);
        Assert.That(product.Name, Is.EqualTo("Sac cuir premium"));

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        var command = new UpdateProductCommand(Guid.NewGuid(), "Sac cuir", "Description.", 15000, Guid.NewGuid(), null, null, null);

        Assert.ThrowsAsync<AppNotFoundException>(async () => await _handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundForASoftDeletedProduct()
    {
        var (productId, categoryId) = SeedProduct(isDeleted: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        var command = new UpdateProductCommand(productId, "Sac cuir", "Description.", 15000, categoryId, null, null, null);

        Assert.ThrowsAsync<AppNotFoundException>(async () => await _handler.Handle(command, CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundWhenCategoryDoesNotExist()
    {
        var (productId, _) = SeedProduct();
        await _context.SaveChangesAsync(CancellationToken.None);

        var command = new UpdateProductCommand(productId, "Sac cuir", "Description.", 15000, Guid.NewGuid(), null, null, null);

        Assert.ThrowsAsync<AppNotFoundException>(async () => await _handler.Handle(command, CancellationToken.None));
    }
}
