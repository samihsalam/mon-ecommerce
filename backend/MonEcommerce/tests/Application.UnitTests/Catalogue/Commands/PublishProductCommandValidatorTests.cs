using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class PublishProductCommandValidatorTests
{
    private PublishProductCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new PublishProductCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new PublishProductCommand(Guid.NewGuid(), true));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        var result = _validator.Validate(new PublishProductCommand(Guid.Empty, true));

        Assert.That(result.IsValid, Is.False);
    }
}
