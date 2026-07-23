using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Infrastructure.Carts;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Carts.Services;

public class CartServiceTests
{
    private ApplicationDbContext _context = null!;
    private ManualTimeProvider _timeProvider = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _timeProvider = new ManualTimeProvider();
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private CartService CreateService() => new(_context, _timeProvider);

    private Product SeedProduct(string name, int priceInCents)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Cat", Slug = "cat" };
        _context.Categories.Add(category);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Description",
            PriceInCents = priceInCents,
            IsPublished = true,
            CategoryId = category.Id,
            Category = category,
        };
        _context.Products.Add(product);
        return product;
    }

    [Test]
    public async Task AddItemAsync_ShouldCreateACartAndAddTheItem()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var cart = await CreateService().AddItemAsync(CartOwner.ForSession("session-1"), product.Id, 2);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items[0].ProductId, Is.EqualTo(product.Id));
        Assert.That(cart.Items[0].Quantity, Is.EqualTo(2));
        Assert.That(cart.Items[0].UnitPriceInCents, Is.EqualTo(5000));
        Assert.That(cart.Items[0].LineTotalInCents, Is.EqualTo(10000));
        Assert.That(cart.TotalInCents, Is.EqualTo(10000));
    }

    [Test]
    public async Task AddItemAsync_ShouldIncrementQuantityWhenTheSameProductIsAddedTwice()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");

        await service.AddItemAsync(owner, product.Id, 2);
        var cart = await service.AddItemAsync(owner, product.Id, 3);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items[0].Quantity, Is.EqualTo(5));
    }

    [Test]
    public void AddItemAsync_ShouldThrowNotFoundExceptionForANonexistentProduct()
    {
        Assert.ThrowsAsync<NotFoundException>(async () =>
            await CreateService().AddItemAsync(CartOwner.ForSession("session-1"), Guid.NewGuid(), 1));
    }

    [Test]
    public async Task UpdateItemQuantityAsync_ShouldRemoveTheItemWhenQuantityIsZero()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        var cart = await service.AddItemAsync(owner, product.Id, 2);

        var updated = await service.UpdateItemQuantityAsync(owner, cart.Items[0].Id, 0);

        Assert.That(updated.Items, Is.Empty);
    }

    [Test]
    public async Task UpdateItemQuantityAsync_ShouldUpdateTheQuantityWhenNonZero()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        var cart = await service.AddItemAsync(owner, product.Id, 2);

        var updated = await service.UpdateItemQuantityAsync(owner, cart.Items[0].Id, 7);

        Assert.That(updated.Items[0].Quantity, Is.EqualTo(7));
    }

    [Test]
    public async Task UpdateItemQuantityAsync_ShouldThrowNotFoundWhenTheItemBelongsToADifferentOwner()
    {
        // IDOR regression: an item id alone must never be enough to mutate someone else's cart —
        // the lookup has to be scoped by owner in the same query.
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var ownerACart = await service.AddItemAsync(CartOwner.ForSession("session-a"), product.Id, 1);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.UpdateItemQuantityAsync(CartOwner.ForSession("session-b"), ownerACart.Items[0].Id, 5));
    }

    [Test]
    public async Task RemoveItemAsync_ShouldRemoveTheItem()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        var cart = await service.AddItemAsync(owner, product.Id, 2);

        var updated = await service.RemoveItemAsync(owner, cart.Items[0].Id);

        Assert.That(updated.Items, Is.Empty);
    }

    [Test]
    public async Task RemoveItemAsync_ShouldThrowNotFoundWhenTheItemBelongsToADifferentOwner()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var ownerACart = await service.AddItemAsync(CartOwner.ForSession("session-a"), product.Id, 1);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await service.RemoveItemAsync(CartOwner.ForSession("session-b"), ownerACart.Items[0].Id));
    }

    [Test]
    public async Task GetCartAsync_ShouldTreatAnAnonymousCartOlderThan24HoursAsExpired()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        await service.AddItemAsync(owner, product.Id, 2);

        _timeProvider.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(1));

        var cart = await service.GetCartAsync(owner);

        Assert.That(cart.Items, Is.Empty);
        Assert.That(cart.TotalInCents, Is.EqualTo(0));
    }

    [Test]
    public async Task GetCartAsync_ShouldNotExpireAnAnonymousCartWithin24Hours()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        await service.AddItemAsync(owner, product.Id, 2);

        _timeProvider.Advance(TimeSpan.FromHours(23));

        var cart = await service.GetCartAsync(owner);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AddItemAsync_ShouldReplaceAnExpiredAnonymousCartRatherThanReuseIt()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForSession("session-1");
        await service.AddItemAsync(owner, product.Id, 2);

        _timeProvider.Advance(TimeSpan.FromHours(25));

        var cart = await service.AddItemAsync(owner, product.Id, 1);

        // A fresh cart, not the expired one with its stale quantity — quantity is 1, not 3.
        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items[0].Quantity, Is.EqualTo(1));
    }

    [Test]
    public async Task GetCartAsync_ShouldNeverExpireAnAuthenticatedCart()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        var owner = CartOwner.ForUser("user-1");
        await service.AddItemAsync(owner, product.Id, 2);

        _timeProvider.Advance(TimeSpan.FromDays(365));

        var cart = await service.GetCartAsync(owner);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MergeAnonymousCartAsync_ShouldSumQuantitiesForDuplicateProductsAndMoveUniqueOnes()
    {
        var shared = SeedProduct("Produit Partagé", 1000);
        var anonymousOnly = SeedProduct("Produit Anonyme", 2000);
        var userOnly = SeedProduct("Produit Utilisateur", 3000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();

        await service.AddItemAsync(CartOwner.ForSession("session-1"), shared.Id, 2);
        await service.AddItemAsync(CartOwner.ForSession("session-1"), anonymousOnly.Id, 1);
        await service.AddItemAsync(CartOwner.ForUser("user-1"), shared.Id, 3);
        await service.AddItemAsync(CartOwner.ForUser("user-1"), userOnly.Id, 5);

        await service.MergeAnonymousCartAsync("session-1", "user-1");

        var mergedCart = await service.GetCartAsync(CartOwner.ForUser("user-1"));
        Assert.That(mergedCart.Items, Has.Count.EqualTo(3));
        Assert.That(mergedCart.Items.First(i => i.ProductId == shared.Id).Quantity, Is.EqualTo(5));
        Assert.That(mergedCart.Items.First(i => i.ProductId == anonymousOnly.Id).Quantity, Is.EqualTo(1));
        Assert.That(mergedCart.Items.First(i => i.ProductId == userOnly.Id).Quantity, Is.EqualTo(5));

        var anonymousCartAfterMerge = await service.GetCartAsync(CartOwner.ForSession("session-1"));
        Assert.That(anonymousCartAfterMerge.Items, Is.Empty);
    }

    [Test]
    public async Task MergeAnonymousCartAsync_ShouldNoOpWhenNoAnonymousCartExists()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        await service.AddItemAsync(CartOwner.ForUser("user-1"), product.Id, 2);

        await service.MergeAnonymousCartAsync("nonexistent-session", "user-1");

        var cart = await service.GetCartAsync(CartOwner.ForUser("user-1"));
        Assert.That(cart.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task MergeAnonymousCartAsync_ShouldNoOpWhenTheAnonymousCartIsAlreadyExpired()
    {
        var product = SeedProduct("Chaise", 5000);
        await _context.SaveChangesAsync(CancellationToken.None);
        var service = CreateService();
        await service.AddItemAsync(CartOwner.ForSession("session-1"), product.Id, 2);

        _timeProvider.Advance(TimeSpan.FromHours(25));

        await service.MergeAnonymousCartAsync("session-1", "user-1");

        var userCart = await service.GetCartAsync(CartOwner.ForUser("user-1"));
        Assert.That(userCart.Items, Is.Empty);
    }

    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
