using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class DeleteCategoryCommandValidatorTests
{
    private DeleteCategoryCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new DeleteCategoryCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new DeleteCategoryCommand(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenIdIsEmpty()
    {
        var result = _validator.Validate(new DeleteCategoryCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }
}
