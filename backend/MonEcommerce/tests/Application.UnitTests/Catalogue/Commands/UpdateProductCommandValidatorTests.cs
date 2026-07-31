using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class UpdateProductCommandValidatorTests
{
    private UpdateProductCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new UpdateProductCommandValidator();

    private static UpdateProductCommand ValidCommand(int priceInCents = 15000) =>
        new(Guid.NewGuid(), "Sac cuir", "Un beau sac en cuir.", priceInCents, Guid.NewGuid(), "Cuir", "Marron", "30x20x10cm");

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(ValidCommand());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenIdIsEmpty()
    {
        var result = _validator.Validate(ValidCommand() with { Id = Guid.Empty });

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenPriceIsZeroOrNegative()
    {
        Assert.That(_validator.Validate(ValidCommand(priceInCents: 0)).IsValid, Is.False);
        Assert.That(_validator.Validate(ValidCommand(priceInCents: -1)).IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenNameIsEmpty()
    {
        var result = _validator.Validate(ValidCommand() with { Name = "" });

        Assert.That(result.IsValid, Is.False);
    }
}
