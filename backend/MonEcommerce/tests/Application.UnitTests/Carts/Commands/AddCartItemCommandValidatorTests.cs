using MonEcommerce.Application.Carts.Commands;
using MonEcommerce.Application.Carts.Models;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Carts.Commands;

public class AddCartItemCommandValidatorTests
{
    private AddCartItemCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new AddCartItemCommandValidator();

    [Test]
    public void ShouldBeValidWithAPositiveQuantityAndProductId()
    {
        var result = _validator.Validate(new AddCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), 1));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        var result = _validator.Validate(new AddCartItemCommand(CartOwner.ForSession("s1"), Guid.Empty, 1));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ShouldFailWhenQuantityIsNotPositive(int quantity)
    {
        var result = _validator.Validate(new AddCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), quantity));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidAtTheMaxQuantityBoundary()
    {
        var result = _validator.Validate(new AddCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), AddCartItemCommandValidator.MaxQuantity));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenQuantityExceedsMaxQuantity()
    {
        var result = _validator.Validate(new AddCartItemCommand(CartOwner.ForSession("s1"), Guid.NewGuid(), AddCartItemCommandValidator.MaxQuantity + 1));

        Assert.That(result.IsValid, Is.False);
    }
}
