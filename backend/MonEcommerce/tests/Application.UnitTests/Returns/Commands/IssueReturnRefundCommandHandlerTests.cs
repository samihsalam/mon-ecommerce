using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Returns.Commands;

public class IssueReturnRefundCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "admin-1";
        public List<string>? Roles { get; set; }
    }

    private ApplicationDbContext _context = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private IssueReturnRefundCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _paymentServiceMock = new Mock<IPaymentService>();
        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");

        _handler = new IssueReturnRefundCommandHandler(
            _context, _paymentServiceMock.Object, _identityServiceMock.Object, new StubUser(), new Mock<ILogger<IssueReturnRefundCommandHandler>>().Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private (Guid ReturnId, Guid OrderId) SeedApprovedReturnWithOrder(string? paymentIntentId = "pi_abc123")
    {
        var address = new Address { Id = Guid.NewGuid(), UserId = "user-1", Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" };
        _context.Addresses.Add(address);
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Status = OrderStatus.Delivered,
            TotalInCents = 5490,
            ShippingAddressId = address.Id,
            StripePaymentIntentId = paymentIntentId,
        };
        _context.Orders.Add(order);
        var returnRequest = new Return
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = "user-1",
            Reason = ReturnReason.WrongSize,
            Description = "Trop petit.",
            Status = ReturnStatus.Approved,
        };
        _context.Returns.Add(returnRequest);
        return (returnRequest.Id, order.Id);
    }

    [Test]
    public async Task Handle_ShouldRefundTheFullOrderAmountAndPersistTheAuditTrailOnSuccess()
    {
        var (returnId, orderId) = SeedApprovedReturnWithOrder();
        await _context.SaveChangesAsync(CancellationToken.None);

        _paymentServiceMock
            .Setup(s => s.CreateRefundAsync("pi_abc123", 5490, It.IsAny<CancellationToken>()))
            .ReturnsAsync("re_refund123");

        await _handler.Handle(new IssueReturnRefundCommand(returnId), CancellationToken.None);

        var returnRequest = await _context.Returns.SingleAsync();
        Assert.That(returnRequest.Status, Is.EqualTo(ReturnStatus.Refunded));

        var auditLog = await _context.PaymentAuditLogs.SingleAsync();
        Assert.That(auditLog.Outcome, Is.EqualTo(PaymentAuditOutcome.Refunded));
        Assert.That(auditLog.AmountInCents, Is.EqualTo(5490));
        Assert.That(auditLog.AdminUserId, Is.EqualTo("admin-1"));
        Assert.That(auditLog.StripeRefundId, Is.EqualTo("re_refund123"));
        Assert.That(auditLog.OrderId, Is.EqualTo(orderId));

        var domainEvent = returnRequest.DomainEvents.OfType<RefundIssuedEvent>().Single();
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
        Assert.That(domainEvent.AmountInCents, Is.EqualTo(5490));
    }

    [Test]
    public async Task Handle_ShouldThrowRefundFailedAndPersistNothingWhenStripeRefundFails()
    {
        var (returnId, _) = SeedApprovedReturnWithOrder();
        await _context.SaveChangesAsync(CancellationToken.None);

        _paymentServiceMock
            .Setup(s => s.CreateRefundAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe unavailable"));

        Assert.ThrowsAsync<RefundFailedException>(async () =>
            await _handler.Handle(new IssueReturnRefundCommand(returnId), CancellationToken.None));

        // AC #3: no partial state persisted.
        Assert.That(await _context.PaymentAuditLogs.AnyAsync(), Is.False);
        var returnRequest = await _context.Returns.SingleAsync();
        Assert.That(returnRequest.Status, Is.EqualTo(ReturnStatus.Approved), "Return status must be untouched on refund failure");
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenReturnIsNotApproved()
    {
        var (returnId, _) = SeedApprovedReturnWithOrder();
        await _context.SaveChangesAsync(CancellationToken.None);
        var returnRequest = await _context.Returns.SingleAsync(r => r.Id == returnId);
        returnRequest.Status = ReturnStatus.Pending;
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new IssueReturnRefundCommand(returnId), CancellationToken.None));

        _paymentServiceMock.Verify(
            s => s.CreateRefundAsync(It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownReturn()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new IssueReturnRefundCommand(Guid.NewGuid()), CancellationToken.None));
    }
}
