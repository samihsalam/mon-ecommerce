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
