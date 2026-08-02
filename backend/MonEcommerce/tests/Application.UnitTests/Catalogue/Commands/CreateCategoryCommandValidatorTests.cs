using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class CreateCategoryCommandValidatorTests
{
    private CreateCategoryCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new CreateCategoryCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new CreateCategoryCommand("Sacs Mode", null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenNameIsEmpty()
    {
        var result = _validator.Validate(new CreateCategoryCommand("", null));

        Assert.That(result.IsValid, Is.False);
    }
}
