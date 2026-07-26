using Moq;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Payments.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Payments.Commands;

public class CreatePaymentIntentCommandHandlerTests
{
    private class StubUser : IUser
    {
        public string? Id { get; set; } = "user-1";
        public List<string>? Roles { get; set; }
    }

    private Mock<ICartService> _cartServiceMock = null!;
    private Mock<IPaymentService> _paymentServiceMock = null!;
    private CreatePaymentIntentCommandHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _cartServiceMock = new Mock<ICartService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _handler = new CreatePaymentIntentCommandHandler(_cartServiceMock.Object, _paymentServiceMock.Object, new StubUser());
    }

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
        _paymentServiceMock
            .Setup(s => s.CreatePaymentIntentAsync(10490, "eur", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("secret_abc", "pi_abc"));

        var response = await _handler.Handle(new CreatePaymentIntentCommand("standard"), CancellationToken.None);

        Assert.That(response.ClientSecret, Is.EqualTo("secret_abc"));
        _paymentServiceMock.Verify(s => s.CreatePaymentIntentAsync(10490, "eur", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Handle_ShouldUseTheExpressShippingCostWhenExpressIsSelected()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CartWith(10000));
        _paymentServiceMock
            .Setup(s => s.CreatePaymentIntentAsync(It.IsAny<long>(), "eur", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentIntentResult("secret_abc", "pi_abc"));

        await _handler.Handle(new CreatePaymentIntentCommand("express"), CancellationToken.None);

        // 10000 (cart) + 990 (express) = 10990
        _paymentServiceMock.Verify(s => s.CreatePaymentIntentAsync(10990, "eur", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Handle_ShouldThrowConflictExceptionForAnEmptyCart()
    {
        _cartServiceMock
            .Setup(s => s.GetCartAsync(It.IsAny<CartOwner>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CartDto([], 0));

        Assert.ThrowsAsync<ConflictException>(async () =>
            await _handler.Handle(new CreatePaymentIntentCommand("standard"), CancellationToken.None));

        _paymentServiceMock.Verify(
            s => s.CreatePaymentIntentAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
