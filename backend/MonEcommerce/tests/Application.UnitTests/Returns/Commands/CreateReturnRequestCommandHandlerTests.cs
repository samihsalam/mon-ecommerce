using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Returns.Commands;

public class CreateReturnRequestCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "user-1";
        public List<string>? Roles { get; set; }
    }

    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public void SetNow(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private ApplicationDbContext _context = null!;
    private Mock<IFileStorageService> _fileStorageServiceMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private ManualTimeProvider _timeProvider = null!;
    private CreateReturnRequestCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");
        _timeProvider = new ManualTimeProvider();

        _handler = new CreateReturnRequestCommandHandler(
            _context, _fileStorageServiceMock.Object, _identityServiceMock.Object, _timeProvider, new StubUser());
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedOrder(string userId, OrderStatus status, DateTimeOffset lastModified)
    {
        var address = new Address { Id = Guid.NewGuid(), UserId = userId, Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" };
        _context.Addresses.Add(address);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = status,
            TotalInCents = 5000,
            ShippingAddressId = address.Id,
            Created = lastModified,
            LastModified = lastModified,
        };
        _context.Orders.Add(order);
        return order.Id;
    }

    [Test]
    public async Task Handle_ShouldCreateAPendingReturnForADeliveredOrderWithinTheWindow()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var orderId = SeedOrder("user-1", OrderStatus.Delivered, now.AddDays(-5));
        await _context.SaveChangesAsync(CancellationToken.None);

        var response = await _handler.Handle(
            new CreateReturnRequestCommand(orderId, ReturnReason.DefectiveProduct, "Le produit est cassé.", []),
            CancellationToken.None);

        Assert.That(response.Status, Is.EqualTo(ReturnStatus.Pending.ToString()));

        var saved = await _context.Returns.SingleAsync();
        Assert.That(saved.OrderId, Is.EqualTo(orderId));
        Assert.That(saved.UserId, Is.EqualTo("user-1"));
        Assert.That(saved.Reason, Is.EqualTo(ReturnReason.DefectiveProduct));
        Assert.That(saved.Status, Is.EqualTo(ReturnStatus.Pending));

        var domainEvent = saved.DomainEvents.OfType<ReturnRequestedEvent>().Single();
        Assert.That(domainEvent.OrderId, Is.EqualTo(orderId));
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
    }

    [Test]
    public async Task Handle_ShouldUploadPhotosAndStoreTheirUrls()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var orderId = SeedOrder("user-1", OrderStatus.Delivered, now.AddDays(-1));
        await _context.SaveChangesAsync(CancellationToken.None);

        _fileStorageServiceMock
            .Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo1.jpg", "returns", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FileUploadResult("https://cdn.example.com/photo1.jpg", "public-id-1"));

        using var stream = new MemoryStream();
        await _handler.Handle(
            new CreateReturnRequestCommand(orderId, ReturnReason.WrongSize, "Trop petit.", [new ReturnPhotoUpload(stream, "photo1.jpg")]),
            CancellationToken.None);

        var saved = await _context.Returns.SingleAsync();
        Assert.That(saved.PhotoUrls, Is.EqualTo(new List<string> { "https://cdn.example.com/photo1.jpg" }));
    }

    [Test]
    public async Task Handle_ShouldThrowReturnWindowExpiredForANonDeliveredOrder()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var orderId = SeedOrder("user-1", OrderStatus.Shipped, now);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ReturnWindowExpiredException>(async () =>
            await _handler.Handle(new CreateReturnRequestCommand(orderId, ReturnReason.Other, "desc", []), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowReturnWindowExpiredWhenDeliveredMoreThan14DaysAgo()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var orderId = SeedOrder("user-1", OrderStatus.Delivered, now.AddDays(-15));
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ReturnWindowExpiredException>(async () =>
            await _handler.Handle(new CreateReturnRequestCommand(orderId, ReturnReason.Other, "desc", []), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldThrowNotFoundForAnotherUsersOrder_ProvingTheIdorGuard()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var orderId = SeedOrder("user-2", OrderStatus.Delivered, now.AddDays(-1));
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new CreateReturnRequestCommand(orderId, ReturnReason.Other, "desc", []), CancellationToken.None));
    }
}
