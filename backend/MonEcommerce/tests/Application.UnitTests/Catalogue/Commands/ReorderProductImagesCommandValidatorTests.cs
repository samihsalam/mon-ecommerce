using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class ReorderProductImagesCommandValidatorTests
{
    private ReorderProductImagesCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new ReorderProductImagesCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new ReorderProductImagesCommand(Guid.NewGuid(), [Guid.NewGuid()]));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        var result = _validator.Validate(new ReorderProductImagesCommand(Guid.Empty, [Guid.NewGuid()]));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenImageIdsIsEmpty()
    {
        var result = _validator.Validate(new ReorderProductImagesCommand(Guid.NewGuid(), []));

        Assert.That(result.IsValid, Is.False);
    }
}
