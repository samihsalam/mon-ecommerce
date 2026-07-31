using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class CreateProductCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private CreateProductCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _handler = new CreateProductCommandHandler(_context);
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
    public async Task Handle_ShouldCreateAnUnpublishedProductWithItsInitialStock()
    {
        var categoryId = SeedCategory();
        await _context.SaveChangesAsync(CancellationToken.None);

        var command = new CreateProductCommand("Sac cuir", "Un beau sac en cuir.", 15000, categoryId, "Cuir", "Marron", "30x20x10cm", 12);

        var productId = await _handler.Handle(command, CancellationToken.None);

        var product = await _context.Products.Include(p => p.Stock).SingleAsync(p => p.Id == productId);
        Assert.That(product.Name, Is.EqualTo("Sac cuir"));
        Assert.That(product.PriceInCents, Is.EqualTo(15000));
        Assert.That(product.CategoryId, Is.EqualTo(categoryId));
        // AC #1: created "Dépublié" — never visible publicly at creation time.
        Assert.That(product.IsPublished, Is.False);
        Assert.That(product.IsDeleted, Is.False);
        Assert.That(product.Stock, Is.Not.Null);
        Assert.That(product.Stock!.Quantity, Is.EqualTo(12));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundWhenCategoryDoesNotExist()
    {
        var command = new CreateProductCommand("Sac cuir", "Un beau sac en cuir.", 15000, Guid.NewGuid(), null, null, null, 0);

        Assert.ThrowsAsync<AppNotFoundException>(async () => await _handler.Handle(command, CancellationToken.None));
    }
}
