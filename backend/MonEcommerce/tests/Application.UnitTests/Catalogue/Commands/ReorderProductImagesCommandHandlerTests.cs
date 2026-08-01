using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class ReorderProductImagesCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private ReorderProductImagesCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new ReorderProductImagesCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private (Guid ProductId, Guid Image1, Guid Image2, Guid Image3) SeedProductWithImages()
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

        var image1 = Guid.NewGuid();
        var image2 = Guid.NewGuid();
        var image3 = Guid.NewGuid();
        _context.ProductImages.Add(new ProductImage { Id = image1, ProductId = product.Id, Url = "1.jpg", PublicId = "p1", DisplayOrder = 0 });
        _context.ProductImages.Add(new ProductImage { Id = image2, ProductId = product.Id, Url = "2.jpg", PublicId = "p2", DisplayOrder = 1 });
        _context.ProductImages.Add(new ProductImage { Id = image3, ProductId = product.Id, Url = "3.jpg", PublicId = "p3", DisplayOrder = 2 });

        return (product.Id, image1, image2, image3);
    }

    [Test]
    public async Task Handle_ShouldPersistTheNewDisplayOrderAndInvalidateTheCatalogueCache()
    {
        var (productId, image1, image2, image3) = SeedProductWithImages();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new ReorderProductImagesCommand(productId, [image3, image1, image2]), CancellationToken.None);

        var images = await _context.ProductImages.Where(i => i.ProductId == productId).ToDictionaryAsync(i => i.Id);
        Assert.That(images[image3].DisplayOrder, Is.EqualTo(0));
        Assert.That(images[image1].DisplayOrder, Is.EqualTo(1));
        Assert.That(images[image2].DisplayOrder, Is.EqualTo(2));

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldThrowValidationWhenTheImageIdSetDoesNotMatch()
    {
        var (productId, image1, image2, _) = SeedProductWithImages();
        await _context.SaveChangesAsync(CancellationToken.None);

        // Missing image3, plus a foreign id — not an exact match of the product's existing images.
        Assert.ThrowsAsync<AppValidationException>(async () =>
            await _handler.Handle(new ReorderProductImagesCommand(productId, [image1, image2, Guid.NewGuid()]), CancellationToken.None));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new ReorderProductImagesCommand(Guid.NewGuid(), [Guid.NewGuid()]), CancellationToken.None));
    }
}
