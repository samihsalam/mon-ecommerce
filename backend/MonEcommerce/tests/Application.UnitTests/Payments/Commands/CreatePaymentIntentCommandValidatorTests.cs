using MonEcommerce.Application.Payments.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Payments.Commands;

public class CreatePaymentIntentCommandValidatorTests
{
    private CreatePaymentIntentCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new CreatePaymentIntentCommandValidator();

    [TestCase("standard")]
    [TestCase("express")]
    public void ShouldBeValidForKnownShippingOptionIds(string shippingOptionId)
    {
        var result = _validator.Validate(new CreatePaymentIntentCommand(shippingOptionId));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenShippingOptionIdIsEmpty()
    {
        var result = _validator.Validate(new CreatePaymentIntentCommand(""));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenShippingOptionIdIsUnknown()
    {
        var result = _validator.Validate(new CreatePaymentIntentCommand("overnight-drone"));

        Assert.That(result.IsValid, Is.False);
    }
}
