using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class DeleteProductImageCommandValidatorTests
{
    private DeleteProductImageCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new DeleteProductImageCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new DeleteProductImageCommand(Guid.NewGuid(), Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        var result = _validator.Validate(new DeleteProductImageCommand(Guid.Empty, Guid.NewGuid()));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenImageIdIsEmpty()
    {
        var result = _validator.Validate(new DeleteProductImageCommand(Guid.NewGuid(), Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }
}
