using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class CreateProductCommandValidatorTests
{
    private CreateProductCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new CreateProductCommandValidator();

    private static CreateProductCommand ValidCommand(int priceInCents = 15000, int initialStock = 10) =>
        new("Sac cuir", "Un beau sac en cuir.", priceInCents, Guid.NewGuid(), "Cuir", "Marron", "30x20x10cm", initialStock);

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenNameIsEmpty()
    {
        var result = _validator.Validate(ValidCommand() with { Name = "" });

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenPriceIsZero()
    {
        // AC #5: price is a positive integer in cents, never negative or zero.
        var result = _validator.Validate(ValidCommand(priceInCents: 0));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenPriceIsNegative()
    {
        var result = _validator.Validate(ValidCommand(priceInCents: -100));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenCategoryIdIsEmpty()
    {
        var result = _validator.Validate(ValidCommand() with { CategoryId = Guid.Empty });

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenInitialStockIsNegative()
    {
        var result = _validator.Validate(ValidCommand(initialStock: -1));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidWhenInitialStockIsZero()
    {
        var result = _validator.Validate(ValidCommand(initialStock: 0));

        Assert.That(result.IsValid, Is.True);
    }
}
