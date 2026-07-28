using MonEcommerce.Application.Payments.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Payments.Commands;

public class CreatePaymentIntentCommandValidatorTests
{
    private CreatePaymentIntentCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new CreatePaymentIntentCommandValidator();

    private static CreatePaymentIntentCommand ValidCommand(string shippingOptionId) =>
        new(shippingOptionId, "12 rue de la Paix", "Paris", "75002", "France");

    [TestCase("standard")]
    [TestCase("express")]
    public void ShouldBeValidForKnownShippingOptionIds(string shippingOptionId)
    {
        var result = _validator.Validate(ValidCommand(shippingOptionId));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenShippingOptionIdIsEmpty()
    {
        var result = _validator.Validate(ValidCommand(""));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenShippingOptionIdIsUnknown()
    {
        var result = _validator.Validate(ValidCommand("overnight-drone"));

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("", "Paris", "75002", "France")]
    [TestCase("12 rue de la Paix", "", "75002", "France")]
    [TestCase("12 rue de la Paix", "Paris", "", "France")]
    [TestCase("12 rue de la Paix", "Paris", "75002", "")]
    public void ShouldFailWhenAnAddressFieldIsEmpty(string street, string city, string postalCode, string country)
    {
        var result = _validator.Validate(new CreatePaymentIntentCommand("standard", street, city, postalCode, country));

        Assert.That(result.IsValid, Is.False);
    }
}
