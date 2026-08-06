using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Account.Queries;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Queries;

public class GetAccountDeletionRequestsQueryHandlerTests
{
    private ApplicationDbContext _context = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private GetAccountDeletionRequestsQueryHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _identityServiceMock = new Mock<IIdentityService>();
        _handler = new GetAccountDeletionRequestsQueryHandler(_context, _identityServiceMock.Object);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    [Test]
    public async Task Handle_ShouldReturnOnlyPendingRequestsOldestFirstWithEmail()
    {
        var oldRequestTime = DateTimeOffset.UtcNow.AddDays(-2);
        var newRequestTime = DateTimeOffset.UtcNow.AddDays(-1);

        _context.AccountDeletionRequests.Add(new AccountDeletionRequest { Id = Guid.NewGuid(), UserId = "user-2", Status = AccountDeletionStatus.Pending, Created = newRequestTime });
        _context.AccountDeletionRequests.Add(new AccountDeletionRequest { Id = Guid.NewGuid(), UserId = "user-1", Status = AccountDeletionStatus.Pending, Created = oldRequestTime });
        _context.AccountDeletionRequests.Add(new AccountDeletionRequest { Id = Guid.NewGuid(), UserId = "user-3", Status = AccountDeletionStatus.Processed, Created = oldRequestTime });
        await _context.SaveChangesAsync(CancellationToken.None);

        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-2")).ReturnsAsync("bob@example.com");

        var result = await _handler.Handle(new GetAccountDeletionRequestsQuery(), CancellationToken.None);

        Assert.That(result.Count, Is.EqualTo(2));
        Assert.That(result[0].UserId, Is.EqualTo("user-1"));
        Assert.That(result[0].Email, Is.EqualTo("alice@example.com"));
        Assert.That(result[1].UserId, Is.EqualTo("user-2"));
        Assert.That(result[1].Email, Is.EqualTo("bob@example.com"));
    }
}
