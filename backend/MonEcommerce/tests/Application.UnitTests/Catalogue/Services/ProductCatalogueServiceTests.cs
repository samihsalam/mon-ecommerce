using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
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

    // Mocked rather than a real ConfigurationBuilder — avoids depending on whatever configuration
    // NuGet packages this test project may or may not transitively reference; IConfiguration's
    // indexer is all GetSitemapEntriesAsync actually reads.
    private static IConfiguration CreateTestConfiguration()
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["Frontend:BaseUrl"]).Returns("https://monecommerce.fr");
        return mock.Object;
    }

    private ProductCatalogueService CreateService(ICacheService? cache = null)
        => new(_context, cache ?? new NullCacheService(), CreateTestConfiguration());

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

        var result = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 20));

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

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 20));

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

        var byCategory = await service.GetProductsAsync(new ProductFilter(chairs.Id, null, null, null, null, null, 1, 20));
        Assert.That(byCategory.Items, Has.Count.EqualTo(2));

        var byMaterial = await service.GetProductsAsync(new ProductFilter(null, "Bois", null, null, null, null, 1, 20));
        Assert.That(byMaterial.Items, Has.Count.EqualTo(2));

        var byColor = await service.GetProductsAsync(new ProductFilter(null, null, "Rouge", null, null, null, 1, 20));
        Assert.That(byColor.Items, Has.Count.EqualTo(2));

        var byPriceRange = await service.GetProductsAsync(new ProductFilter(null, null, null, 10000, 20000, null, 1, 20));
        Assert.That(byPriceRange.Items, Has.Count.EqualTo(2));

        var combined = await service.GetProductsAsync(new ProductFilter(chairs.Id, "Bois", "Rouge", null, null, null, 1, 20));
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

        var negativePage = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, null, -1, 20));
        Assert.That(negativePage.PageNumber, Is.EqualTo(1));

        var oversizedPageSize = await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 1000));
        Assert.That(oversizedPageSize.PageSize, Is.EqualTo(100));
    }

    [Test]
    public async Task GetProductsAsync_ShouldNotOverflowOnAnExtremePageNumber()
    {
        // Regression test: (pageNumber - 1) * pageSize previously overflowed 32-bit int
        // arithmetic for extreme pageNumber values, wrapping to a negative Skip() that SQL
        // Server would reject at runtime. Just proving this doesn't throw is the point.
        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, null, int.MaxValue, 100));

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

        var resultA = await service.GetProductsAsync(new ProductFilter(null, "a:col=b", "c", null, null, null, 1, 20));
        var resultB = await service.GetProductsAsync(new ProductFilter(null, "a", "b:col=c", null, null, null, 1, 20));

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

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 20));

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

        await CreateService(cacheMock.Object).GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 20));

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
            [new ProductSummaryDto(Guid.NewGuid(), "Cached Product", 99999, null, null, null, Guid.NewGuid(), "Cached Category", "cached-category", true)],
            1, 1, 20, 1);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);
        cacheMock
            .Setup(c => c.GetAsync<PagedProductsResult<ProductSummaryDto>>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cannedResult);

        var result = await CreateService(cacheMock.Object).GetProductsAsync(new ProductFilter(null, null, null, null, null, null, 1, 20));

        Assert.That(result, Is.SameAs(cannedResult));
        cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedProductsResult<ProductSummaryDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Test]
    public async Task GetProductsAsync_ShouldMatchOnNameOrDescriptionCaseInsensitively()
    {
        var category = SeedCategory();
        var sofa = SeedProduct(category, "Canapé en Cuir", 50000);
        var chair = SeedProduct(category, "Chaise en Bois", 10000);
        chair.Description = "Une chaise avec des accents en CUIR véritable";
        SeedProduct(category, "Table Basse", 15000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, "cuir", 1, 20));

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(i => i.Name), Is.EquivalentTo(new[] { sofa.Name, chair.Name }));
    }

    [Test]
    public async Task GetProductsAsync_ShouldRankExactAndPrefixNameMatchesAboveDescriptionOnlyMatches()
    {
        var category = SeedCategory();
        var descriptionOnly = SeedProduct(category, "Table Basse", 15000);
        descriptionOnly.Description = "Fabriquée en chêne massif";
        var prefixMatch = SeedProduct(category, "Chêne Massif Buffet", 30000);
        var exactMatch = SeedProduct(category, "chêne", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, "chêne", 1, 20));

        Assert.That(result.Items.Select(i => i.Name), Is.EqualTo(new[] { exactMatch.Name, prefixMatch.Name, descriptionOnly.Name }));
    }

    [Test]
    public async Task GetProductsAsync_ShouldReturnEmptyResultWhenSearchTermMatchesNothing()
    {
        var category = SeedCategory();
        SeedProduct(category, "Canapé", 50000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(null, null, null, null, null, "zzz-no-match", 1, 20));

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetProductsAsync_ShouldCombineSearchWithOtherFilters()
    {
        var chairs = SeedCategory("Chaises");
        var tables = SeedCategory("Tables");
        SeedProduct(chairs, "Chaise en Cuir", 10000);
        SeedProduct(tables, "Table en Cuir", 20000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductsAsync(new ProductFilter(chairs.Id, null, null, null, null, "cuir", 1, 20));

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Name, Is.EqualTo("Chaise en Cuir"));
    }

    [Test]
    public async Task GetSearchSuggestionsAsync_ShouldReturnMatchingCategoriesAndProductNames()
    {
        var chairs = SeedCategory("Chaises en cuir");
        SeedCategory("Tables");
        SeedProduct(chairs, "Fauteuil Cuir", 10000);
        SeedProduct(chairs, "Canapé Cuir", 50000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSearchSuggestionsAsync("cuir");

        Assert.That(result.Categories, Is.EqualTo(new[] { "Chaises en cuir" }));
        Assert.That(result.Products, Is.EquivalentTo(new[] { "Fauteuil Cuir", "Canapé Cuir" }));
    }

    [Test]
    public async Task GetSearchSuggestionsAsync_ShouldCapCombinedSuggestionsAtFive()
    {
        var category = SeedCategory("cuiristerie");
        for (var i = 0; i < 6; i++)
        {
            SeedProduct(category, $"Produit Cuir {i}", 1000);
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSearchSuggestionsAsync("cuir");

        Assert.That(result.Categories.Count + result.Products.Count, Is.EqualTo(5));
    }

    [Test]
    public async Task GetSearchSuggestionsAsync_ShouldExcludeUnpublishedProducts()
    {
        var category = SeedCategory("Divers");
        SeedProduct(category, "Cuir Publié", 1000, isPublished: true);
        SeedProduct(category, "Cuir Brouillon", 1000, isPublished: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSearchSuggestionsAsync("cuir");

        Assert.That(result.Products, Is.EqualTo(new[] { "Cuir Publié" }));
    }

    [Test]
    public async Task GetProductsAsync_ShouldShareTheSameCacheEntryForCaseAndWhitespaceVariantsOfTheSameSearchTerm()
    {
        // Regression: BuildCacheKey previously hashed filter.Search AS-IS (raw casing/whitespace),
        // while the actual query normalizes it (Trim().ToLowerInvariant()) — "Cuir", "cuir", and
        // " cuir " produce identical results but were fragmenting into separate cache entries.
        var category = SeedCategory();
        SeedProduct(category, "Canapé Cuir", 50000);
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

        await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, "Cuir", 1, 20));
        await service.GetProductsAsync(new ProductFilter(null, null, null, null, null, " cuir ", 1, 20));

        cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<PagedProductsResult<ProductSummaryDto>>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetCategoriesAsync_ShouldReturnAllCategoriesOrderedByName()
    {
        SeedCategory("Tables");
        SeedCategory("Chaises");
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetCategoriesAsync();

        Assert.That(result.Select(c => c.Name), Is.EqualTo(new[] { "Chaises", "Tables" }));
    }

    [Test]
    public async Task GetProductByIdAsync_ShouldReturnTheFullDetailShapeWithImagesOrderedByDisplayOrder()
    {
        var category = SeedCategory("Sacs");
        var product = SeedProduct(category, "Tote Parisienne", 28500, material: "Cuir", color: "Cognac", stockQuantity: 3);
        product.Dimensions = "30x20x10cm";
        _context.ProductImages.Add(new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "second.webp", DisplayOrder = 2 });
        _context.ProductImages.Add(new ProductImage { Id = Guid.NewGuid(), ProductId = product.Id, Url = "first.webp", DisplayOrder = 1 });
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductByIdAsync(product.Id);

        Assert.That(result.Id, Is.EqualTo(product.Id));
        Assert.That(result.Name, Is.EqualTo("Tote Parisienne"));
        Assert.That(result.Description, Is.EqualTo("Description"));
        Assert.That(result.PriceInCents, Is.EqualTo(28500));
        Assert.That(result.Material, Is.EqualTo("Cuir"));
        Assert.That(result.Dimensions, Is.EqualTo("30x20x10cm"));
        Assert.That(result.StockQuantity, Is.EqualTo(3));
        Assert.That(result.InStock, Is.True);
        Assert.That(result.CategoryId, Is.EqualTo(category.Id));
        Assert.That(result.CategoryName, Is.EqualTo("Sacs"));
        Assert.That(result.CategorySlug, Is.EqualTo("sacs"));
        Assert.That(result.ImageUrls, Is.EqualTo(new[] { "first.webp", "second.webp" }));
    }

    [Test]
    public void GetProductByIdAsync_ShouldThrowNotFoundExceptionForANonexistentId()
    {
        Assert.ThrowsAsync<NotFoundException>(async () => await CreateService().GetProductByIdAsync(Guid.NewGuid()));
    }

    [Test]
    public async Task GetProductByIdAsync_ShouldThrowNotFoundExceptionForAnUnpublishedProduct()
    {
        var category = SeedCategory();
        var product = SeedProduct(category, "Draft Product", 1000, isPublished: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<NotFoundException>(async () => await CreateService().GetProductByIdAsync(product.Id));
    }

    [Test]
    public async Task GetProductByIdAsync_ShouldReturnZeroStockAndNotInStockWhenNoStockRowExists()
    {
        var category = SeedCategory();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "No Stock Row",
            Description = "Description",
            PriceInCents = 1000,
            IsPublished = true,
            CategoryId = category.Id,
            Category = category,
        };
        _context.Products.Add(product);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetProductByIdAsync(product.Id);

        Assert.That(result.StockQuantity, Is.EqualTo(0));
        Assert.That(result.InStock, Is.False);
    }

    [Test]
    public async Task GetProductByIdAsync_ShouldPopulateAndReuseTheCache()
    {
        var category = SeedCategory();
        var product = SeedProduct(category, "Cached Product", 1000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var cacheMock = new Mock<ICacheService>();
        var store = new Dictionary<string, object?>();
        cacheMock
            .Setup(c => c.GetAsync<int?>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => (int?)store.GetValueOrDefault(key));
        cacheMock
            .Setup(c => c.GetAsync<ProductDetailDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => (ProductDetailDto?)store.GetValueOrDefault(key));
        cacheMock
            .Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<ProductDetailDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback<string, ProductDetailDto, TimeSpan, CancellationToken>((key, value, _, _) => store[key] = value)
            .Returns(Task.CompletedTask);

        var service = CreateService(cacheMock.Object);

        await service.GetProductByIdAsync(product.Id);
        await service.GetProductByIdAsync(product.Id);

        cacheMock.Verify(
            c => c.SetAsync(It.IsAny<string>(), It.IsAny<ProductDetailDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task GetSimilarProductsAsync_ShouldReturnUpToFourPublishedProductsFromTheSameCategoryExcludingSelf()
    {
        var chairs = SeedCategory("Chaises");
        var tables = SeedCategory("Tables");
        var source = SeedProduct(chairs, "Chaise A", 1000);
        SeedProduct(chairs, "Chaise B", 1000);
        SeedProduct(chairs, "Chaise C", 1000);
        SeedProduct(chairs, "Chaise D", 1000);
        SeedProduct(chairs, "Chaise E", 1000);
        SeedProduct(tables, "Table A", 1000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSimilarProductsAsync(source.Id);

        Assert.That(result, Has.Count.EqualTo(4));
        Assert.That(result.Select(r => r.Id), Does.Not.Contain(source.Id));
        Assert.That(result.All(r => r.Name.StartsWith("Chaise")), Is.True);
    }

    [Test]
    public async Task GetSimilarProductsAsync_ShouldReturnFewerThanFourWhenFewerSiblingsExist()
    {
        var category = SeedCategory();
        var source = SeedProduct(category, "Source", 1000);
        SeedProduct(category, "Sibling A", 1000);
        SeedProduct(category, "Sibling B", 1000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSimilarProductsAsync(source.Id);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetSimilarProductsAsync_ShouldExcludeUnpublishedCandidates()
    {
        var category = SeedCategory();
        var source = SeedProduct(category, "Published Source", 1000);
        SeedProduct(category, "Unpublished Sibling", 1000, isPublished: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSimilarProductsAsync(source.Id);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSimilarProductsAsync_ShouldReturnEmptyForANonexistentOrUnpublishedSourceProduct()
    {
        var category = SeedCategory();
        var unpublishedSource = SeedProduct(category, "Draft", 1000, isPublished: false);
        SeedProduct(category, "Sibling", 1000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var forMissingId = await CreateService().GetSimilarProductsAsync(Guid.NewGuid());
        var forUnpublishedSource = await CreateService().GetSimilarProductsAsync(unpublishedSource.Id);

        Assert.That(forMissingId, Is.Empty);
        Assert.That(forUnpublishedSource, Is.Empty);
    }

    [Test]
    public async Task GetSitemapEntriesAsync_ShouldListOnlyPublishedProductsWithCorrectUrlAndLastModified()
    {
        // SeedCategory's Slug is a plain ToLowerInvariant() of the name (not run through
        // SlugHelper) — "Sacs" is used here so the category segment needs no slugification of its
        // own, keeping this test focused on the PRODUCT name's slugification (the accented "Tote
        // Élégante" is the part that actually exercises SlugHelper.Slugify).
        var category = SeedCategory("Sacs");
        var published = SeedProduct(category, "Tote Élégante", 1000);
        published.LastModified = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);
        SeedProduct(category, "Draft Bag", 1000, isPublished: false);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetSitemapEntriesAsync();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Url, Is.EqualTo($"https://monecommerce.fr/catalogue/sacs/tote-elegante-{published.Id}"));
        Assert.That(result[0].LastModified, Is.EqualTo(published.LastModified));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void GetSitemapEntriesAsync_ShouldThrowClearlyWhenFrontendBaseUrlIsMissingOrEmpty(string? baseUrl)
    {
        // Regression: a bare `_configuration["Frontend:BaseUrl"]!` previously let a missing config
        // key throw a bare NullReferenceException out of this public, unauthenticated,
        // crawler-facing endpoint — this asserts the clearer, deliberate failure instead.
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Frontend:BaseUrl"]).Returns(baseUrl);
        var service = new ProductCatalogueService(_context, new NullCacheService(), configMock.Object);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.GetSitemapEntriesAsync());
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
