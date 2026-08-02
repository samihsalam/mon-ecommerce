using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MonEcommerce.Application.Orders.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using MonEcommerce.Infrastructure.Orders;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.Services;

public class AdminOrderServiceTests
{
    private ApplicationDbContext _context = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private AdminOrderService _service = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // A real UserStore backed by the same InMemory context — not a Mock<UserManager<...>> —
        // so _userManager.Users genuinely queries seeded ApplicationUser rows (AdminOrderService
        // relies on this IQueryable for its batched customer-name lookups and search).
        var userStore = new UserStore<ApplicationUser>(_context);
        _userManager = new UserManager<ApplicationUser>(
            userStore, null!, new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);

        _service = new AdminOrderService(_context, _userManager);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _userManager.Dispose();
    }

    private Guid SeedUserAndAddress(string userId, string name)
    {
        _context.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com", Name = name });

        var address = new Address { Id = Guid.NewGuid(), UserId = userId, Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" };
        _context.Addresses.Add(address);
        return address.Id;
    }

    private Order SeedOrder(string userId, Guid addressId, DateTimeOffset created, OrderStatus status = OrderStatus.Pending, int totalInCents = 1000)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            TotalInCents = totalInCents,
            ShippingAddressId = addressId,
            Created = created,
        };
        _context.Orders.Add(order);
        return order;
    }

    private static AdminOrderFilter EmptyFilter(int pageNumber = 1, int pageSize = 20) =>
        new(null, null, null, null, pageNumber, pageSize);

    [Test]
    public async Task GetOrdersAsync_ShouldReturnOrdersSortedByDateDescendingByDefault()
    {
        var addressId = SeedUserAndAddress("user-1", "Alice Martin");
        var older = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-2));
        var newer = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-1));
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter());

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].Id, Is.EqualTo(newer.Id));
        Assert.That(result.Items[1].Id, Is.EqualTo(older.Id));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldIncludeOrderNumberCustomerNameAndFrenchStatusLabel()
    {
        var addressId = SeedUserAndAddress("user-1", "Salma Benali");
        var order = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow, OrderStatus.Shipped, 28500);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter());

        var dto = result.Items.Single();
        Assert.That(dto.OrderNumber, Is.EqualTo($"#{order.Id.ToString("N")[..8].ToUpperInvariant()}"));
        Assert.That(dto.CustomerName, Is.EqualTo("Salma Benali"));
        Assert.That(dto.TotalInCents, Is.EqualTo(28500));
        Assert.That(dto.Status, Is.EqualTo("Expédiée"));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldFilterByStatus()
    {
        var addressId = SeedUserAndAddress("user-1", "Alice Martin");
        SeedOrder("user-1", addressId, DateTimeOffset.UtcNow, OrderStatus.Pending);
        var shipped = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow, OrderStatus.Shipped);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter() with { Status = OrderStatus.Shipped });

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo(shipped.Id));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldFilterByDateRange()
    {
        var addressId = SeedUserAndAddress("user-1", "Alice Martin");
        SeedOrder("user-1", addressId, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var inRange = SeedOrder("user-1", addressId, new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));
        SeedOrder("user-1", addressId, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter() with
        {
            DateFrom = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            DateTo = new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo(inRange.Id));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldFilterByCustomerNameSearch()
    {
        var addressId1 = SeedUserAndAddress("user-1", "Salma Benali");
        var addressId2 = SeedUserAndAddress("user-2", "Karim Dupont");
        var salmaOrder = SeedOrder("user-1", addressId1, DateTimeOffset.UtcNow);
        SeedOrder("user-2", addressId2, DateTimeOffset.UtcNow);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter() with { Search = "salma" });

        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items[0].Id, Is.EqualTo(salmaOrder.Id));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldReturnPaginationMetadata()
    {
        var addressId = SeedUserAndAddress("user-1", "Alice Martin");
        for (var i = 0; i < 5; i++)
        {
            SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-i));
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetOrdersAsync(EmptyFilter(pageNumber: 2, pageSize: 2));

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.TotalCount, Is.EqualTo(5));
        Assert.That(result.PageNumber, Is.EqualTo(2));
        Assert.That(result.PageSize, Is.EqualTo(2));
        Assert.That(result.TotalPages, Is.EqualTo(3));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldReturnEmptyResultForAnEmptyDatabase()
    {
        var result = await _service.GetOrdersAsync(EmptyFilter());

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
        Assert.That(result.TotalPages, Is.EqualTo(0));
    }
}
