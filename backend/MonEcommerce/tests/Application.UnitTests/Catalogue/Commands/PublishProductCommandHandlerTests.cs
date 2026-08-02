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

public class PublishProductCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private PublishProductCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new PublishProductCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedProduct(bool withImage, bool isPublished = false)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Sac cuir",
            Description = "Un beau sac.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
            IsPublished = isPublished,
        };
        if (withImage)
        {
            product.Images.Add(new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "1.jpg", PublicId = "p1", DisplayOrder = 0 });
        }
        _context.Products.Add(product);
        return product.Id;
    }

    [Test]
    public async Task Handle_ShouldPublishAProductWithAtLeastOneImage()
    {
        var productId = SeedProduct(withImage: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new PublishProductCommand(productId, true), CancellationToken.None);

        Assert.That(result.IsPublished, Is.True);
        var product = await _context.Products.SingleAsync(p => p.Id == productId);
        Assert.That(product.IsPublished, Is.True);

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldBlockPublishingAProductWithNoImages()
    {
        var productId = SeedProduct(withImage: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<AppValidationException>(async () =>
            await _handler.Handle(new PublishProductCommand(productId, true), CancellationToken.None));

        var product = await _context.Products.SingleAsync(p => p.Id == productId);
        Assert.That(product.IsPublished, Is.False);

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Handle_ShouldUnpublishAProductRegardlessOfImages()
    {
        var productId = SeedProduct(withImage: false, isPublished: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new PublishProductCommand(productId, false), CancellationToken.None);

        Assert.That(result.IsPublished, Is.False);
        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownProduct()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new PublishProductCommand(Guid.NewGuid(), true), CancellationToken.None));
    }
}
