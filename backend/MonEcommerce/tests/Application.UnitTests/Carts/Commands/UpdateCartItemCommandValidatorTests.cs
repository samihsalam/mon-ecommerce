using MonEcommerce.Application.Carts.Commands;
using MonEcommerce.Application.Carts.Models;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Carts.Commands;

public class UpdateCartItemCommandValidatorTests
{
    private UpdateCartItemCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new UpdateCartItemCommandValidator();

    [Test]
    public void ShouldBeValidWithAPositiveQuantity()
    {
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), 3));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldBeValidWithZeroQuantity()
    {
        // Zero is the documented "remove this item" contract (AC #3) — must NOT be rejected.
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), 0));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWithANegativeQuantity()
    {
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), -1));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenItemIdIsEmpty()
    {
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.Empty, 1));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidAtTheMaxQuantityBoundary()
    {
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), AddCartItemCommandValidator.MaxQuantity));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenQuantityExceedsMaxQuantity()
    {
        var result = _validator.Validate(new UpdateCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), AddCartItemCommandValidator.MaxQuantity + 1));

        Assert.That(result.IsValid, Is.False);
    }
}
