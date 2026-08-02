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

public class DeleteCategoryCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private DeleteCategoryCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new DeleteCategoryCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedCategory()
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Sacs", Slug = "sacs" };
        _context.Categories.Add(category);
        return category.Id;
    }

    [Test]
    public async Task Handle_ShouldDeleteAnEmptyCategoryAndInvalidateTheCache()
    {
        var categoryId = SeedCategory();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new DeleteCategoryCommand(categoryId), CancellationToken.None);

        Assert.That(await _context.Categories.AnyAsync(c => c.Id == categoryId), Is.False);
        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenTheCategoryHasChildren()
    {
        var categoryId = SeedCategory();
        _context.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Sous-catégorie", Slug = "sous-categorie", ParentId = categoryId });
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new DeleteCategoryCommand(categoryId), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenTheCategoryHasPublishedProducts()
    {
        var categoryId = SeedCategory();
        _context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Sac", Description = "Desc", PriceInCents = 1000, CategoryId = categoryId, IsPublished = true });
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new DeleteCategoryCommand(categoryId), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenTheCategoryHasOnlyUnpublishedProducts()
    {
        var categoryId = SeedCategory();
        _context.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Sac", Description = "Desc", PriceInCents = 1000, CategoryId = categoryId, IsPublished = false });
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new DeleteCategoryCommand(categoryId), CancellationToken.None));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownCategory()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new DeleteCategoryCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
