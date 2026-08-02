using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using MonEcommerce.Infrastructure.Identity;
using MonEcommerce.Infrastructure.Returns;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.Services;

public class AdminReturnServiceTests
{
    private ApplicationDbContext _context = null!;
    private UserManager<ApplicationUser> _userManager = null!;
    private AdminReturnService _service = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // A real UserStore backed by the same InMemory context — see AdminOrderServiceTests
        // (Story 7.1) for why this isn't a Mock<UserManager<...>>.
        var userStore = new UserStore<ApplicationUser>(_context);
        _userManager = new UserManager<ApplicationUser>(
            userStore, null!, new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<ApplicationUser>>.Instance);

        _service = new AdminReturnService(_context, _userManager);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _userManager.Dispose();
    }

    private void SeedUser(string userId, string name) =>
        _context.Users.Add(new ApplicationUser { Id = userId, UserName = $"{userId}@example.com", Email = $"{userId}@example.com", Name = name });

    private Return SeedReturn(string userId, Guid orderId, DateTimeOffset created, ReturnStatus status = ReturnStatus.Pending, ReturnReason reason = ReturnReason.WrongSize, List<string>? photoUrls = null)
    {
        var returnRequest = new Return
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = userId,
            Reason = reason,
            Description = "Description.",
            PhotoUrls = photoUrls ?? [],
            Status = status,
            Created = created,
        };
        _context.Returns.Add(returnRequest);
        return returnRequest;
    }

    private static AdminReturnFilter EmptyFilter() => new(null, null, null);

    [Test]
    public async Task GetReturnsAsync_ShouldIncludeOrderNumberCustomerNameReasonLabelAndPhotos()
    {
        SeedUser("user-1", "Salma Benali");
        var orderId = Guid.NewGuid();
        SeedReturn("user-1", orderId, DateTimeOffset.UtcNow, ReturnStatus.Pending, ReturnReason.DefectiveProduct, ["https://cdn.example.com/1.jpg"]);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetReturnsAsync(EmptyFilter());

        var dto = result.Single();
        Assert.That(dto.OrderNumber, Is.EqualTo($"#{orderId.ToString("N")[..8].ToUpperInvariant()}"));
        Assert.That(dto.CustomerName, Is.EqualTo("Salma Benali"));
        Assert.That(dto.Reason, Is.EqualTo("Produit défectueux"));
        Assert.That(dto.Status, Is.EqualTo("En attente"));
        Assert.That(dto.PhotoUrls, Is.EqualTo(new List<string> { "https://cdn.example.com/1.jpg" }));
    }

    [Test]
    public async Task GetReturnsAsync_ShouldSortByDateDescending()
    {
        SeedUser("user-1", "Alice Martin");
        var older = SeedReturn("user-1", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-2));
        var newer = SeedReturn("user-1", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-1));
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetReturnsAsync(EmptyFilter());

        Assert.That(result[0].Id, Is.EqualTo(newer.Id));
        Assert.That(result[1].Id, Is.EqualTo(older.Id));
    }

    [Test]
    public async Task GetReturnsAsync_ShouldFilterByStatus()
    {
        SeedUser("user-1", "Alice Martin");
        SeedReturn("user-1", Guid.NewGuid(), DateTimeOffset.UtcNow, ReturnStatus.Pending);
        var approved = SeedReturn("user-1", Guid.NewGuid(), DateTimeOffset.UtcNow, ReturnStatus.Approved);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetReturnsAsync(EmptyFilter() with { Status = ReturnStatus.Approved });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(approved.Id));
    }

    [Test]
    public async Task GetReturnsAsync_ShouldFilterByDateRange()
    {
        SeedUser("user-1", "Alice Martin");
        SeedReturn("user-1", Guid.NewGuid(), new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var inRange = SeedReturn("user-1", Guid.NewGuid(), new DateTimeOffset(2026, 4, 5, 0, 0, 0, TimeSpan.Zero));
        SeedReturn("user-1", Guid.NewGuid(), new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _service.GetReturnsAsync(EmptyFilter() with
        {
            DateFrom = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            DateTo = new DateTimeOffset(2026, 4, 12, 0, 0, 0, TimeSpan.Zero),
        });

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Id, Is.EqualTo(inRange.Id));
    }

    [Test]
    public async Task GetReturnsAsync_ShouldReturnEmptyListForAnEmptyDatabase()
    {
        var result = await _service.GetReturnsAsync(EmptyFilter());

        Assert.That(result, Is.Empty);
    }
}
