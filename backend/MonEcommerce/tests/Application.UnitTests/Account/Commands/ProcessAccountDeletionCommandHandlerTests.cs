using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MonEcommerce.Application.Account.Commands;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.UnitTests.Account.Commands;

public class ProcessAccountDeletionCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "admin-1";
        public List<string>? Roles { get; set; }
    }

    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public void SetNow(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private ApplicationDbContext _context = null!;
    private Mock<IIdentityService> _identityServiceMock = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private ManualTimeProvider _timeProvider = null!;
    private ProcessAccountDeletionCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _identityServiceMock = new Mock<IIdentityService>();
        _identityServiceMock.Setup(s => s.GetEmailAsync("user-1")).ReturnsAsync("alice@example.com");
        _identityServiceMock
            .Setup(s => s.AnonymizeUserAsync("user-1", "Utilisateur supprimé", It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _paymentServiceMock = new Mock<IPaymentService>();
        _timeProvider = new ManualTimeProvider();

        _handler = new ProcessAccountDeletionCommandHandler(
            _context,
            _identityServiceMock.Object,
            _paymentServiceMock.Object,
            _timeProvider,
            new StubUser(),
            Mock.Of<ILogger<ProcessAccountDeletionCommandHandler>>());
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Guid SeedPendingRequest(string userId = "user-1")
    {
        var request = new AccountDeletionRequest { Id = Guid.NewGuid(), UserId = userId, Status = AccountDeletionStatus.Pending };
        _context.AccountDeletionRequests.Add(request);
        return request.Id;
    }

    [Test]
    public async Task Handle_ShouldAnonymizeTheUserAndMarkTheRequestProcessed()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var requestId = SeedPendingRequest();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None);

        _identityServiceMock.Verify(s => s.AnonymizeUserAsync("user-1", "Utilisateur supprimé", It.Is<string>(e => e.EndsWith("@deleted.invalid"))), Times.Once);

        var saved = await _context.AccountDeletionRequests.SingleAsync();
        Assert.That(saved.Status, Is.EqualTo(AccountDeletionStatus.Processed));
        Assert.That(saved.ProcessedByAdminUserId, Is.EqualTo("admin-1"));
        Assert.That(saved.ProcessedAt, Is.EqualTo(now));
    }

    // Review finding: Order.ShippingAddressId has OnDelete(DeleteBehavior.Restrict) to Address —
    // hard-deleting the row would throw an FK-constraint violation for any customer with order
    // history (and AC #3 requires order records to be RETAINED). The address row must survive,
    // with its content scrubbed instead.
    [Test]
    public async Task Handle_ShouldAnonymizeRatherThanDeleteTheUsersAddresses()
    {
        var requestId = SeedPendingRequest();
        var addressId = Guid.NewGuid();
        _context.Addresses.Add(new Address { Id = addressId, UserId = "user-1", Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" });
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None);

        var address = await _context.Addresses.SingleAsync(a => a.Id == addressId);
        Assert.That(address.Street, Is.EqualTo("Adresse supprimée"));
        Assert.That(address.City, Is.Empty);
        Assert.That(address.PostalCode, Is.Empty);
        Assert.That(address.Country, Is.Empty);
    }

    // Reproduces the exact scenario the review finding identified: a customer with a real order
    // referencing the address being anonymized must not hit the FK-Restrict violation, and the
    // Order must keep resolving to a real Address row (AC #3 — order records retained).
    [Test]
    public async Task Handle_ShouldNotBreakOrdersReferencingTheAnonymizedAddress()
    {
        var requestId = SeedPendingRequest();
        var addressId = Guid.NewGuid();
        _context.Addresses.Add(new Address { Id = addressId, UserId = "user-1", Street = "1 Rue de Paris", City = "Paris", PostalCode = "75001", Country = "France" });
        _context.Orders.Add(new Order { Id = Guid.NewGuid(), UserId = "user-1", ShippingAddressId = addressId, TotalInCents = 5000 });
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.DoesNotThrowAsync(async () => await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None));

        var order = await _context.Orders.SingleAsync();
        Assert.That(order.ShippingAddressId, Is.EqualTo(addressId));
        Assert.That(await _context.Addresses.AnyAsync(a => a.Id == addressId), Is.True);
    }

    [Test]
    public async Task Handle_ShouldRevokeActiveRefreshTokensForTheUser()
    {
        var now = DateTimeOffset.UtcNow;
        _timeProvider.SetNow(now);
        var requestId = SeedPendingRequest();
        _context.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = "user-1", Token = "tok-1", ExpiresAt = now.AddDays(30), CreatedAt = now });
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None);

        var token = await _context.RefreshTokens.SingleAsync();
        Assert.That(token.RevokedAt, Is.EqualTo(now));
    }

    [Test]
    public async Task Handle_ShouldRequestStripeCustomerDataDeletionWithTheOriginalEmail()
    {
        var requestId = SeedPendingRequest();
        await _context.SaveChangesAsync(CancellationToken.None);

        await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None);

        _paymentServiceMock.Verify(p => p.DeleteCustomerDataAsync("alice@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldStillCompleteWhenStripeDeletionFails()
    {
        _paymentServiceMock
            .Setup(p => p.DeleteCustomerDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Stripe unavailable"));

        var requestId = SeedPendingRequest();
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.DoesNotThrowAsync(async () => await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None));

        var saved = await _context.AccountDeletionRequests.SingleAsync();
        Assert.That(saved.Status, Is.EqualTo(AccountDeletionStatus.Processed));
    }

    [Test]
    public async Task Handle_ShouldThrowConflictWhenTheRequestIsAlreadyProcessed()
    {
        var request = new AccountDeletionRequest { Id = Guid.NewGuid(), UserId = "user-1", Status = AccountDeletionStatus.Processed };
        _context.AccountDeletionRequests.Add(request);
        await _context.SaveChangesAsync(CancellationToken.None);

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new ProcessAccountDeletionCommand(request.Id), CancellationToken.None));
    }

    [Test]
    public void Handle_ShouldThrowNotFoundForAnUnknownRequest()
    {
        Assert.ThrowsAsync<AppNotFoundException>(async () =>
            await _handler.Handle(new ProcessAccountDeletionCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Test]
    public void Handle_ShouldThrowWhenAnonymizationFails()
    {
        _identityServiceMock
            .Setup(s => s.AnonymizeUserAsync("user-1", "Utilisateur supprimé", It.IsAny<string>()))
            .ReturnsAsync(Result.Failure(["Erreur Identity."]));

        var requestId = SeedPendingRequest();
        _context.SaveChanges();

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new ProcessAccountDeletionCommand(requestId), CancellationToken.None));
    }
}
