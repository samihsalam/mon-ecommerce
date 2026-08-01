using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class AddProductImageCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IFileStorageService> _fileStorageServiceMock = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private AddProductImageCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new AddProductImageCommandHandler(_context, _fileStorageServiceMock.Object, _catalogueServiceMock.Object);
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
            IsDeleted = isDeleted,
        };
        _context.Products.Add(product);
        return product.Id;
    }

    [Test]
    public async Task Handle_ShouldUploadWithProductGalleryPresetAndPersistTheImage()
    {
        var productId = SeedProduct();
        await _context.SaveChangesAsync(CancellationToken.None);

        _fileStorageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo.jpg", "products", ImageTransformPreset.ProductGallery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("https://cdn.example.com/photo.jpg", "public-id-1"));

        using var stream = new MemoryStream();
        var result = await _handler.Handle(new AddProductImageCommand(productId, new ProductImageUpload(stream, "photo.jpg")), CancellationToken.None);

        Assert.That(result.Url, Is.EqualTo("https://cdn.example.com/photo.jpg"));
        Assert.That(result.DisplayOrder, Is.EqualTo(0));

        var image = await _context.ProductImages.SingleAsync();
        Assert.That(image.PublicId, Is.EqualTo("public-id-1"));
        Assert.That(image.ProductId, Is.EqualTo(productId));

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldAppendNewImagesAfterExistingOnes()
    {
        var productId = SeedProduct();
        _context.ProductImages.Add(new ProductImage { Id = Guid.NewGuid(), ProductId = productId, Url = "https://cdn.example.com/1.jpg", PublicId = "p1", DisplayOrder = 0 });
        _context.ProductImages.Add(new ProductImage { Id = Guid.NewGuid(), ProductId = productId, Url = "https://cdn.example.com/2.jpg", PublicId = "p2", DisplayOrder = 1 });
        await _context.SaveChangesAsync(CancellationToken.None);

        _fileStorageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo3.jpg", "products", ImageTransformPreset.ProductGallery, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("https://cdn.example.com/3.jpg", "public-id-3"));

        using var stream = new MemoryStream();
        var result = await _handler.Handle(new AddProductImageCommand(productId, new ProductImageUpload(stream, "photo3.jpg")), CancellationToken.None);

        Assert.That(result.DisplayOrder, Is.EqualTo(2));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        using var stream = new MemoryStream();
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new AddProductImageCommand(Guid.NewGuid(), new ProductImageUpload(stream, "photo.jpg")), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundForASoftDeletedProduct()
    {
        var productId = SeedProduct(isDeleted: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        using var stream = new MemoryStream();
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new AddProductImageCommand(productId, new ProductImageUpload(stream, "photo.jpg")), CancellationToken.None));
    }
}
