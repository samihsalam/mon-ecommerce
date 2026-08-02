using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Catalogue;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Services;

public class AnalyticsServiceTests
{
    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public void SetNow(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private ApplicationDbContext _context = null!;
    private ManualTimeProvider _timeProvider = null!;

    private static readonly DateTimeOffset Now = new(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _timeProvider = new ManualTimeProvider();
        _timeProvider.SetNow(Now);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private static IConfiguration CreateConfiguration()
    {
        var mock = new Mock<IConfiguration>();
        mock.Setup(c => c["Frontend:BaseUrl"]).Returns("https://admin.example.com");
        return mock.Object;
    }

    private AnalyticsService CreateService(ICacheService? cache = null) =>
        new(_context, cache ?? new NullCacheServiceStub(), CreateConfiguration(), _timeProvider);

    private Guid SeedProduct(string name = "Sac cuir", int stockQuantity = 10, int alertThreshold = 5, bool isDeleted = false)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Description.",
            PriceInCents = 15000,
            CategoryId = Guid.NewGuid(),
            IsDeleted = isDeleted,
            Stock = new Stock { Id = Guid.NewGuid(), Quantity = stockQuantity, AlertThreshold = alertThreshold },
        };
        _context.Products.Add(product);
        return product.Id;
    }

    private void SeedView(Guid productId, DateOnly date, int count) =>
        _context.ProductDailyViewCounts.Add(new ProductDailyViewCount { Id = Guid.NewGuid(), ProductId = productId, Date = date, ViewCount = count });

    private Order SeedOrderWithItem(Guid productId, DateTimeOffset created, int quantity, OrderStatus status = OrderStatus.Pending)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Status = status,
            TotalInCents = 1000,
            ShippingAddressId = Guid.NewGuid(),
            Created = created,
        };
        _context.Orders.Add(order);
        _context.OrderItems.Add(new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = productId, ProductName = "Sac", UnitPriceInCents = 1000, Quantity = quantity });
        return order;
    }

    [Test]
    public async Task GetTopProductsAsync_ShouldSumViewsWithinTheLastSevenDaysOnly()
    {
        var productId = SeedProduct();
        SeedView(productId, DateOnly.FromDateTime(Now.UtcDateTime), 5);
        SeedView(productId, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-3), 3);
        // Older than 7 days — must not count.
        SeedView(productId, DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-10), 100);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetTopProductsAsync();

        var entry = result.MostViewed.Single();
        Assert.That(entry.Count, Is.EqualTo(8));
    }

    [Test]
    public async Task GetTopProductsAsync_ShouldSumUnitsSoldWithinTheLastSevenDaysExcludingCancelledOrders()
    {
        var productId = SeedProduct();
        SeedOrderWithItem(productId, Now.AddDays(-2), 3);
        SeedOrderWithItem(productId, Now.AddDays(-1), 2, OrderStatus.Cancelled); // excluded
        SeedOrderWithItem(productId, Now.AddDays(-10), 99); // too old — excluded
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetTopProductsAsync();

        var entry = result.BestSelling.Single();
        Assert.That(entry.Count, Is.EqualTo(3));
    }

    [Test]
    public async Task GetTopProductsAsync_ShouldLimitEachListToTenProducts()
    {
        for (var i = 0; i < 12; i++)
        {
            var productId = SeedProduct($"Produit {i}");
            SeedView(productId, DateOnly.FromDateTime(Now.UtcDateTime), i + 1);
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetTopProductsAsync();

        Assert.That(result.MostViewed, Has.Count.EqualTo(10));
        // Highest counts first.
        Assert.That(result.MostViewed[0].Count, Is.EqualTo(12));
    }

    [Test]
    public async Task GetTopProductsAsync_ShouldReadFromAndWriteToTheCache()
    {
        var productId = SeedProduct();
        SeedView(productId, DateOnly.FromDateTime(Now.UtcDateTime), 5);
        await _context.SaveChangesAsync(CancellationToken.None);

        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<TopProductsDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((TopProductsDto?)null);

        await CreateService(cacheMock.Object).GetTopProductsAsync();

        cacheMock.Verify(c => c.GetAsync<TopProductsDto>("analytics:top-products", It.IsAny<CancellationToken>()), Times.Once);
        cacheMock.Verify(c => c.SetAsync("analytics:top-products", It.IsAny<TopProductsDto>(), TimeSpan.FromHours(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task GetTopProductsAsync_ShouldReturnTheCachedValueWithoutRecomputing()
    {
        var cachedResult = new TopProductsDto([new ProductAnalyticsSummaryDto(Guid.NewGuid(), "Cached Product", 999)], []);
        var cacheMock = new Mock<ICacheService>();
        cacheMock.Setup(c => c.GetAsync<TopProductsDto>(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(cachedResult);

        var result = await CreateService(cacheMock.Object).GetTopProductsAsync();

        Assert.That(result, Is.SameAs(cachedResult));
        cacheMock.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<TopProductsDto>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetLowStockProductsAsync_ShouldReturnProductsAtOrBelowTheirAlertThreshold()
    {
        var lowStockId = SeedProduct("Sac cuir", stockQuantity: 3, alertThreshold: 5);
        SeedProduct("Sac toile", stockQuantity: 20, alertThreshold: 5); // well-stocked — excluded
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetLowStockProductsAsync();

        var entry = result.Single();
        Assert.That(entry.ProductId, Is.EqualTo(lowStockId));
        Assert.That(entry.CurrentStock, Is.EqualTo(3));
        Assert.That(entry.AlertThreshold, Is.EqualTo(5));
        Assert.That(entry.EditUrl, Is.EqualTo($"https://admin.example.com/admin/produits/{lowStockId}"));
    }

    [Test]
    public async Task GetLowStockProductsAsync_ShouldExcludeSoftDeletedProducts()
    {
        SeedProduct("Sac supprimé", stockQuantity: 1, alertThreshold: 5, isDeleted: true);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await CreateService().GetLowStockProductsAsync();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetLowStockProductsAsync_ShouldNeverTouchTheCache()
    {
        SeedProduct("Sac cuir", stockQuantity: 1, alertThreshold: 5);
        await _context.SaveChangesAsync(CancellationToken.None);

        var cacheMock = new Mock<ICacheService>(MockBehavior.Strict);

        await CreateService(cacheMock.Object).GetLowStockProductsAsync();

        cacheMock.VerifyNoOtherCalls();
    }

    // A minimal, always-miss ICacheService for tests that don't care about caching behavior —
    // NullCacheService (Infrastructure/ExternalServices) is internal-equivalent but this keeps the
    // test file self-contained without depending on its exact accessibility.
    private sealed class NullCacheServiceStub : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    }
}
