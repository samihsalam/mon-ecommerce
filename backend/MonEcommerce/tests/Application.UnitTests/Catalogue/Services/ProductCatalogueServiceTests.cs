using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Catalogue;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.ExternalServices;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Services;

public class ProductCatalogueServiceTests
{
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private ProductCatalogueService CreateService(ICacheService? cache = null)
        => new(_context, cache ?? new NullCacheService());

    private Category SeedCategory(string name = "Chaises")
    {
        var category = new Category { Id = Guid.NewGuid(), Name = name, Slug = name.ToLowerInvariant() };
        _context.Categories.Add(category);
        return category;
    }

    private Product SeedProduct(
        Category category,
        string name,
        int priceInCents,
        string? material = null,
        string? color = null,
        bool isPublished = true,
        int stockQuantity = 5)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Description",
            PriceInCents = priceInCents,
            Material = material,
            Color = color,
            IsPublished = isPublished,
            CategoryId = category.Id,
            Category = category,
        };
        _context.Products.Add(product);
        _context.Stocks.Add(new Stock { Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Quantity = stockQuantity });
        return product;
    }

    [Test]
    public async Task GetProductsAsync_ShouldReturnEmptyResultForAnEmptyCatalogue()
    {
        var service = CreateService();

        var result = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 20));

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
        Assert.That(result.TotalPages, Is.EqualTo(0));
    }

    [Test]
    public async Task GetProductsAsync_ShouldExcludeUnpublishedProducts()
    {
        var category = SeedCategory();
        SeedProduct(category, "Published Chair", 10000, isPublished: true);
        SeedProduct(category, "Draft Chair", 10000, isPublished: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 20));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Is.EqualTo("Published Chair"));
    }

    [Test]
    public async Task GetProductsAsync_ShouldFilterByCategoryMaterialColorAndPriceRange()
    {
        var chairs = SeedCategory("Chaises");
        var tables = SeedCategory("Tables");
        SeedProduct(chairs, "Wooden Red Chair", 15000, material: "Bois", color: "Rouge");
        SeedProduct(chairs, "Metal Blue Chair", 8000, material: "Metal", color: "Bleu");
        SeedProduct(tables, "Wooden Red Table", 15000, material: "Bois", color: "Rouge");
        await _context.SaveChangesAsync(CancellationToken.None);

        var service = CreateService();

        var byCategory = await service.GetProductsAsync(new ProductFilter(chairs.Id, null, null, null, null, 1, 20));
        Assert.That(byCategory.Items, Has.Count.EqualTo(2));

        var byMaterial = await service.GetProductsAsync(new ProductFilter(null, "Bois", null, null, null, 1, 20));
        Assert.That(byMaterial.Items, Has.Count.EqualTo(2));

        var byColor = await service.GetProductsAsync(new ProductFilter(null, null, "Rouge", null, null, 1, 20));
        Assert.That(byColor.Items, Has.Count.EqualTo(2));

        var byPriceRange = await service.GetProductsAsync(new ProductFilter(null, null, null, 10000, 20000, 1, 20));
        Assert.That(byPriceRange.Items, Has.Count.EqualTo(2));

        var combined = await service.GetProductsAsync(new ProductFilter(chairs.Id, "Bois", "Rouge", null, null, 1, 20));
        Assert.That(combined.Items, Has.Count.EqualTo(1));
        Assert.That(combined.Items[0].Name, Is.EqualTo("Wooden Red Chair"));
    }

    [Test]
    public async Task GetProductsAsync_ShouldClampPageNumberAndPageSize()
    {
        var category = SeedCategory();
        for (var i = 0; i < 5; i++)
        {
            SeedProduct(category, $"Product {i}", 1000);
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var service = CreateService();

        var negativePage = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, -1, 20));
        Assert.That(negativePage.PageNumber, Is.EqualTo(1));

        var oversizedPageSize = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 1000));
        Assert.That(oversizedPageSize.PageSize, Is.EqualTo(100));
    }

    [Test]
    public async Task GetProductsAsync_ShouldNotOverflowOnAnExtremePageNumber()
    {
        // Regression test: (pageNumber - 1) * pageSize previously overflowed 32-bit int
        // arithmetic for extreme pageNumber values, wrapping to a negative Skip() that SQL
        // Server would reject at runtime. Just proving this doesn't throw is the point.
        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, int.MaxValue, 100));

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.PageNumber, Is.LessThanOrEqualTo(1_000_000));
    }

    [Test]
    public async Task GetProductsAsync_ShouldNotCollideCacheKeysWhenFilterValuesContainDelimiterLikeSubstrings()
    {
        var category = SeedCategory();
        SeedProduct(category, "Product A", 1000, material: "a:col=b", color: "c");
        SeedProduct(category, "Product B", 1000, material: "a", color: "b:col=c");
        await _context.SaveChangesAsync(CancellationToken.None);

        var cacheMock = new Mock<ICacheService>();
        var store = new Dictionary<string, object?>();
        cacheMock
            .Setup(c => c.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => (int?)store.GetValueOrDefault(key));
        cacheMock
            .Setup(c => c.GetAsync<PagedProductsResult<ProductSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => (PagedProductsResult<ProductSummaryDto>?)store.GetValueOrDefault(key));
        cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedProductsResult<ProductSummaryDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, PagedProductsResult<ProductSummaryDto>, TimeSpan, CancellationToken>((key, value, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        var service = CreateService(cacheMock.Object);

        var resultA = await service.GetProductsAsync(new ProductFilter(null, "a:col=b", "c", null, null, 1, 20));
        var resultB = await service.GetProductsAsync(new ProductFilter(null, "a", "b:col=c", null, null, 1, 20));

        Assert.That(resultA.Items, Has.Count.EqualTo(1));
        Assert.That(resultA.Items[0].Name, Is.EqualTo("Product A"));
        Assert.That(resultB.Items, Has.Count.EqualTo(1));
        Assert.That(resultB.Items[0].Name, Is.EqualTo("Product B"));
    }

    [Test]
    public async Task GetProductsAsync_ShouldComputeTotalPagesCorrectly()
    {
        var category = SeedCategory();
        for (var i = 0; i < 25; i++)
        {
            SeedProduct(category, $"Product {i}", 1000);
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 20));

        Assert.That(result.TotalCount, Is.EqualTo(25));
        Assert.That(result.TotalPages, Is.EqualTo(2));
        Assert.That(result.Items, Has.Count.EqualTo(20));
    }

    [Test]
    public async Task GetProductsAsync_ShouldPopulateTheCacheOnAMiss()
    {
        var category = SeedCategory();
        SeedProduct(category, "Chair", 1000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        cacheMock
            .Setup(c => c.GetAsync<PagedProductsResult<ProductSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PagedProductsResult<ProductSummaryDto>?)null);

        await CreateService(cacheMock.Object).GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 20));

        cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedProductsResult<ProductSummaryDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetProductsAsync_ShouldReturnTheCachedResultOnAHitWithoutRecomputing()
    {
        // A canned result the (empty) database alone could never produce — proves the cached
        // value was actually returned, not silently recomputed from the DB.
        var cannedResult = new PagedProductsResult<ProductSummaryDto>(
            [new ProductSummaryDto(Guid.NewGuid(), "Cached Product", 99999, null, null, null, Guid.NewGuid(), "Cached Category", true)],
            1, 1, 20, 1);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        cacheMock
            .Setup(c => c.GetAsync<PagedProductsResult<ProductSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cannedResult);

        var result = await CreateService(cacheMock.Object).GetProductsAsync(new ProductFilter(null, null, null, null, null, 1, 20));

        Assert.That(result, Is.SameAs(cannedResult));
        cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedProductsResult<ProductSummaryDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task InvalidateCatalogueCacheAsync_ShouldBumpTheVersionSoAStaleCacheKeyIsNoLongerReachable()
    {
        var cacheMock = new Mock<ICacheService>();
        var storedVersion = 1;
        cacheMock
            .Setup(c => c.GetAsync<int?>("catalogue:version", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => storedVersion);
        cacheMock
            .Setup(c => c.SetAsync("catalogue:version", It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, int, TimeSpan, CancellationToken>((_, value, _, _) => storedVersion = value)
            .Returns(Task.CompletedTask);

        var service = CreateService(cacheMock.Object);

        await service.InvalidateCatalogueCacheAsync();

        Assert.That(storedVersion, Is.EqualTo(2));
    }
}
