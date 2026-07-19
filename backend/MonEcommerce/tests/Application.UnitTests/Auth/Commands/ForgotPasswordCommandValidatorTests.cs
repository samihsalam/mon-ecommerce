using MonEcommerce.Application.Auth.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Commands;

public class ForgotPasswordCommandValidatorTests
{
    private ForgotPasswordCommandValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new ForgotPasswordCommandValidator();
    }

    [Test]
    public void ShouldBeValidWhenEmailIsCorrect()
    {
        var command = new ForgotPasswordCommand("alice@example.com");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsEmpty()
    {
        var command = new ForgotPasswordCommand("");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenEmailIsInvalid()
    {
        var command = new ForgotPasswordCommand("not-an-email");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
    }
}
