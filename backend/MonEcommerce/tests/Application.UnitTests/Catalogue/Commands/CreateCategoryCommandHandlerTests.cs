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

public class CreateCategoryCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IProductCatalogueService> _catalogueServiceMock = null!;
    private CreateCategoryCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _catalogueServiceMock = new Mock<IProductCatalogueService>();

        _handler = new CreateCategoryCommandHandler(_context, _catalogueServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task Handle_ShouldGenerateAKebabCaseSlugFromTheName()
    {
        var result = await _handler.Handle(new CreateCategoryCommand("Sacs Mode", null), CancellationToken.None);

        Assert.That(result.Slug, Is.EqualTo("sacs-mode"));
        Assert.That(result.ParentId, Is.Null);

        _catalogueServiceMock.Verify(s => s.InvalidateCatalogueCacheAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldLinkToAnExistingParentCategory()
    {
        var parent = new Category { Id = Guid.NewGuid(), Name = "Sacs", Slug = "sacs" };
        _context.Categories.Add(parent);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new CreateCategoryCommand("Sacs à main", parent.Id), CancellationToken.None);

        Assert.That(result.ParentId, Is.EqualTo(parent.Id));

        var saved = await _context.Categories.SingleAsync(c => c.Id == result.Id);
        Assert.That(saved.ParentId, Is.EqualTo(parent.Id));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownParent()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new CreateCategoryCommand("Sacs à main", Guid.NewGuid()), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenTheGeneratedSlugAlreadyExists()
    {
        _context.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "Sacs Mode", Slug = "sacs-mode" });
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new CreateCategoryCommand("Sacs Mode", null), CancellationToken.None));
    }
}
