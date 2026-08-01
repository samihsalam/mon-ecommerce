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

public class DeleteProductImageCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IFileStorageService> _fileStorageServiceMock = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private DeleteProductImageCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new DeleteProductImageCommandHandler(_context, _fileStorageServiceMock.Object, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private (Guid ProductId, Guid ImageId) SeedProductWithImage()
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
        };
        _context.Products.Add(product);

        var image = new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "1.jpg", PublicId = "public-id-1", DisplayOrder = 0 };
        _context.ProductImages.Add(image);

        return (product.Id, image.Id);
    }

    [Test]
    public async Task Handle_ShouldDeleteFromCloudinaryAndRemoveTheRowAndInvalidateTheCache()
    {
        var (productId, imageId) = SeedProductWithImage();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DeleteProductImageCommand(productId, imageId), CancellationToken.None);

        _fileStorageServiceMock.Verify(s => s.DeleteAsync("public-id-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(await _context.ProductImages.AnyAsync(i => i.Id == imageId), Is.False);
        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundWhenTheImageBelongsToADifferentProduct()
    {
        var (_, imageId) = SeedProductWithImage();
        await _context.SaveChangesAsync(CancellationToken.None);

        // IDOR-style attempt: correct imageId, wrong productId.
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new DeleteProductImageCommand(Guid.NewGuid(), imageId), CancellationToken.None));

        _fileStorageServiceMock.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownImage()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new DeleteProductImageCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
    }
}
