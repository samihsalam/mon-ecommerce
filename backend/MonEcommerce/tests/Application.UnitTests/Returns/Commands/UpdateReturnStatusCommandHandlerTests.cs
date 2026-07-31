using Microsoft.EntityFrameworkCore;
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

public class UpdateReturnStatusCommandHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private UpdateReturnStatusCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");

        _handler = new UpdateReturnStatusCommandHandler(_context, _identityServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedReturn(ReturnStatus status = ReturnStatus.Pending)
    {
        var returnRequest = new Return
        {
            Id = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            UserId = "user-1",
            Reason = ReturnReason.WrongSize,
            Description = "Trop petit.",
            Status = status,
        };
        _context.Returns.Add(returnRequest);
        return returnRequest.Id;
    }

    [Test]
    public async Task Handle_ShouldApproveAPendingReturnAndPublishReturnStatusUpdated()
    {
        var returnId = SeedReturn();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateReturnStatusCommand(returnId, ReturnStatus.Approved), CancellationToken.None);

        var returnRequest = await _context.Returns.SingleAsync();
        Assert.That(returnRequest.Status, Is.EqualTo(ReturnStatus.Approved));

        var domainEvent = returnRequest.DomainEvents.OfType<ReturnStatusUpdatedEvent>().Single();
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
        Assert.That(domainEvent.NewStatus, Is.EqualTo("Validé"));
    }

    [Test]
    public async Task Handle_ShouldRejectAPendingReturn()
    {
        var returnId = SeedReturn();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new UpdateReturnStatusCommand(returnId, ReturnStatus.Rejected), CancellationToken.None);

        var returnRequest = await _context.Returns.SingleAsync();
        Assert.That(returnRequest.Status, Is.EqualTo(ReturnStatus.Rejected));
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenReturnIsNotPending()
    {
        var returnId = SeedReturn(ReturnStatus.Approved);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new UpdateReturnStatusCommand(returnId, ReturnStatus.Rejected), CancellationToken.None));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownReturn()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Approved), CancellationToken.None));
    }
}
