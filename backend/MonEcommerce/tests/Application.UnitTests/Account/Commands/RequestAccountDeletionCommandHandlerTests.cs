using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Account.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Commands;

public class RequestAccountDeletionCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "user-1";
        public List<string>? Roles { get; set; }
    }

    private ApplicationDbContext _context = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private RequestAccountDeletionCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");

        _handler = new RequestAccountDeletionCommandHandler(_context, _identityServiceMock.Object, new StubUser());
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task Handle_ShouldCreateAPendingRequestAndRaiseTheDomainEvent()
    {
        var requestId = await _handler.Handle(new RequestAccountDeletionCommand(), CancellationToken.None);

        var saved = await _context.AccountDeletionRequests.SingleAsync();
        Assert.That(saved.Id, Is.EqualTo(requestId));
        Assert.That(saved.UserId, Is.EqualTo("user-1"));
        Assert.That(saved.Status, Is.EqualTo(AccountDeletionStatus.Pending));

        var domainEvent = saved.DomainEvents.OfType<AccountDeletionRequestedEvent>().Single();
        Assert.That(domainEvent.CustomerEmail, Is.EqualTo("alice@example.com"));
        Assert.That(domainEvent.RequestId, Is.EqualTo(requestId));
    }

    [Test]
    public void Handle_ShouldThrowConflictWhenAPendingRequestAlreadyExists()
    {
        _context.AccountDeletionRequests.Add(new AccountDeletionRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Status = AccountDeletionStatus.Pending,
        });
        _context.SaveChanges();

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new RequestAccountDeletionCommand(), CancellationToken.None));
    }

    [Test]
    public async Task Handle_ShouldAllowANewRequestWhenThePreviousOneWasAlreadyProcessed()
    {
        _context.AccountDeletionRequests.Add(new AccountDeletionRequest
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Status = AccountDeletionStatus.Processed,
        });
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new RequestAccountDeletionCommand(), CancellationToken.None);

        var pendingCount = await _context.AccountDeletionRequests.CountAsync(r => r.Status == AccountDeletionStatus.Pending);
        Assert.That(pendingCount, Is.EqualTo(1));
    }
}
