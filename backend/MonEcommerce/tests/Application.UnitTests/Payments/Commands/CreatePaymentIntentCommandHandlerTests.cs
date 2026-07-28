using Microsoft.EntityFrameworkCore;
using Moq;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Payments.Commands;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Payments.Commands;

public class CreatePaymentIntentCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "user-1";
        public List<string>? Roles { get; set; }
    }

    private ApplicationDbContext _context = null!;
    private Mock<ICartService> _cartServiceMock = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private CreatePaymentIntentCommandHandler _handler = null!;

    private static readonly CreatePaymentIntentCommand StandardCommand =
        new("standard", "12 rue de la Paix", "Paris", "75002", "France");

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _cartServiceMock = new Mock<ICartService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _handler = new CreatePaymentIntentCommandHandler(_cartServiceMock.Object, _paymentServiceMock.Object, _context, new StubUser());

        _paymentServiceMock
            .Setup(s => s.CreatePaymentIntentAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("secret_abc", "pi_abc"));
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private static CartDto CartWith(int totalInCents, int itemCount = 1)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(_ => new CartItemDto(Guid.NewGuid(), Guid.NewGuid(), "Chaise", null, 5000, 1, 5000))
            .ToList();
        return new CartDto(items, totalInCents);
    }

    [Test]
    public async Task Handle_ShouldCreateAPaymentIntentForCartTotalPlusShippingCost()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.Is<CartOwner>(o => o.UserId == "user-1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(10000));

        var response = await _handler.Handle(StandardCommand, CancellationToken.None);

        Assert.That(response.ClientSecret, Is.EqualTo("secret_abc"));
        _paymentServiceMock.Verify(
            s => s.CreatePaymentIntentAsync(10490, "eur", It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task Handle_ShouldUseTheExpressShippingCostWhenExpressIsSelected()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(10000));

        await _handler.Handle(StandardCommand with { ShippingOptionId = "express" }, CancellationToken.None);

        // 10000 (cart) + 990 (express) = 10990
        _paymentServiceMock.Verify(
            s => s.CreatePaymentIntentAsync(10990, "eur", It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public void Handle_ShouldThrowConflictExceptionForAnEmptyCart()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartDto([], 0));

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(StandardCommand, CancellationToken.None));

        _paymentServiceMock.Verify(
            s => s.CreatePaymentIntentAsync(
                It.IsAny<long>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Story 4.6: the address must be persisted (the webhook that eventually confirms this
    // payment can't reach the client-side CheckoutStore) and its id + the userId + shipping
    // option id must ride on the PaymentIntent's metadata, since that's the only channel the
    // asynchronous webhook has to reconstruct what order to create.
    [Test]
    public async Task Handle_ShouldPersistTheAddressAndCarryItsIdOnThePaymentIntentMetadata()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(10000));

        await _handler.Handle(StandardCommand, CancellationToken.None);

        var savedAddress = await _context.Addresses.SingleAsync();
        Assert.That(savedAddress.UserId, Is.EqualTo("user-1"));
        Assert.That(savedAddress.Street, Is.EqualTo("12 rue de la Paix"));
        Assert.That(savedAddress.City, Is.EqualTo("Paris"));

        _paymentServiceMock.Verify(
            s => s.CreatePaymentIntentAsync(
                It.IsAny<long>(),
                It.IsAny<string>(),
                It.Is<IReadOnlyDictionary<string, string>?>(m =>
                    m != null &&
                    m["userId"] == "user-1" &&
                    m["shippingAddressId"] == savedAddress.Id.ToString() &&
                    m["shippingOptionId"] == "standard"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
