using MonEcommerce.Application.Orders.Commands;
using MonEcommerce.Domain.Enums;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.Commands;

public class UpdateOrderStatusCommandValidatorTests
{
    private UpdateOrderStatusCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new UpdateOrderStatusCommandValidator();

    [Test]
    public void ShouldFailWhenMovingToShippedWithoutATrackingNumber()
    {
        var result = _validator.Validate(new UpdateOrderStatusCommand(Guid.NewGuid(), OrderStatus.Shipped, null));

        Assert.That(result.IsValid, Is.False);
        // AC #2's exact wording.
        Assert.That(result.Errors[0].ErrorMessage, Is.EqualTo("Le numéro de suivi est requis pour le statut Expédiée"));
    }

    [Test]
    public void ShouldPassWhenMovingToShippedWithATrackingNumber()
    {
        var result = _validator.Validate(new UpdateOrderStatusCommand(Guid.NewGuid(), OrderStatus.Shipped, "TRACK1"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldPassWhenMovingToDeliveredWithoutATrackingNumber()
    {
        var result = _validator.Validate(new UpdateOrderStatusCommand(Guid.NewGuid(), OrderStatus.Delivered, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenOrderIdIsEmpty()
    {
        var result = _validator.Validate(new UpdateOrderStatusCommand(Guid.Empty, OrderStatus.Processing, null));

        Assert.That(result.IsValid, Is.False);
    }
}
