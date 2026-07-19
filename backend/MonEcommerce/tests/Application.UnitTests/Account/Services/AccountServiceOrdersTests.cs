using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Services;

public class AccountServiceOrdersTests
{
    private ApplicationDbContext _context = null!;
    private Mock<UserManager<ApplicationUser>> _userManager = null!;
    private AccountService _accountService = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        var storeMock = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(storeMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _accountService = new AccountService(_userManager.Object, _context);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedAddress(string userId)
    {
        var address = new Address
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Street = "1 Rue de Paris",
            City = "Paris",
            PostalCode = "75001",
            Country = "France",
        };
        _context.Addresses.Add(address);
        return address.Id;
    }

    private Order SeedOrder(string userId, Guid addressId, DateTimeOffset created, OrderStatus status = OrderStatus.Pending, int totalInCents = 1000, string? trackingNumber = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            TotalInCents = totalInCents,
            ShippingAddressId = addressId,
            TrackingNumber = trackingNumber,
            Created = created,
        };
        _context.Orders.Add(order);
        return order;
    }

    [Test]
    public async Task GetOrdersAsync_ShouldReturnEmptyListWhenUserHasNoOrders()
    {
        var result = await _accountService.GetOrdersAsync("user-1", 1, 10);

        Assert.That(result.Items, Is.Empty);
        Assert.That(result.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldSortByDateDescendingAndOnlyReturnTheRequestingUsersOwnOrders()
    {
        var addressId = SeedAddress("user-1");
        SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-2));
        var newest = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-1));
        var otherUserAddressId = SeedAddress("user-2");
        SeedOrder("user-2", otherUserAddressId, DateTimeOffset.UtcNow);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _accountService.GetOrdersAsync("user-1", 1, 10);

        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items[0].Id, Is.EqualTo(newest.Id));
        Assert.That(result.TotalCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetOrdersAsync_ShouldPaginateCorrectly()
    {
        var addressId = SeedAddress("user-1");
        for (var i = 0; i < 5; i++)
        {
            SeedOrder("user-1", addressId, DateTimeOffset.UtcNow.AddDays(-i));
        }
        await _context.SaveChangesAsync(CancellationToken.None);

        var page1 = await _accountService.GetOrdersAsync("user-1", 1, 2);
        var page2 = await _accountService.GetOrdersAsync("user-1", 2, 2);

        Assert.That(page1.Items, Has.Count.EqualTo(2));
        Assert.That(page2.Items, Has.Count.EqualTo(2));
        Assert.That(page1.TotalCount, Is.EqualTo(5));
        Assert.That(page2.TotalCount, Is.EqualTo(5));
        Assert.That(page1.Items[0].Id, Is.Not.EqualTo(page2.Items[0].Id));
    }

    [Test]
    public async Task GetOrderDetailAsync_ShouldReturnFullDetailForTheOwner()
    {
        var addressId = SeedAddress("user-1");
        var order = SeedOrder("user-1", addressId, DateTimeOffset.UtcNow, OrderStatus.Shipped, 2500, "TRACK123");
        _context.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ProductId = Guid.NewGuid(),
            ProductName = "T-shirt",
            UnitPriceInCents = 2500,
            Quantity = 1,
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        var detail = await _accountService.GetOrderDetailAsync("user-1", order.Id);

        Assert.That(detail.Status, Is.EqualTo("Expédiée"));
        Assert.That(detail.TrackingNumber, Is.EqualTo("TRACK123"));
        Assert.That(detail.ShippingAddress.City, Is.EqualTo("Paris"));
        Assert.That(detail.Items, Has.Count.EqualTo(1));
        Assert.That(detail.Items[0].ProductName, Is.EqualTo("T-shirt"));
    }

    [Test]
    public void GetOrderDetailAsync_ShouldThrowNotFoundForANonExistentOrder()
    {
        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _accountService.GetOrderDetailAsync("user-1", Guid.NewGuid()));
    }

    [Test]
    public async Task GetOrderDetailAsync_ShouldThrowNotFoundForAnotherUsersOrder_ProvingTheIdorGuard()
    {
        var addressId = SeedAddress("user-2");
        var order = SeedOrder("user-2", addressId, DateTimeOffset.UtcNow);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await _accountService.GetOrderDetailAsync("user-1", order.Id));
    }
}
