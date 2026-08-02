using MonEcommerce.Application.Catalogue.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Catalogue.Commands;

public class UpdateStockCommandValidatorTests
{
    private UpdateStockCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new UpdateStockCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.NewGuid(), 10, 5, "Réapprovisionnement"));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldBeValidWithoutAReason()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.NewGuid(), 10, 5, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenQuantityIsNegative()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.NewGuid(), -1, 5, null));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidWhenQuantityIsZero()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.NewGuid(), 0, 5, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenAlertThresholdIsNegative()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.NewGuid(), 10, -1, null));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenProductIdIsEmpty()
    {
        var result = _validator.Validate(new UpdateStockCommand(Guid.Empty, 10, 5, null));

        Assert.That(result.IsValid, Is.False);
    }
}
