using MonEcommerce.Application.Auth.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Commands;

public class LoginCommandValidatorTests
{
    private LoginCommandValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new LoginCommandValidator();
    }

    [Test]
    public void ShouldBeValidWhenAllFieldsAreCorrect()
    {
        var command = new LoginCommand("alice@example.com", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsEmpty()
    {
        var command = new LoginCommand("", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(LoginCommand.Email)), Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsInvalid()
    {
        var command = new LoginCommand("not-an-email", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(LoginCommand.Email)), Is.True);
    }

    [Test]
    public void ShouldFailWhenPasswordIsEmpty()
    {
        var command = new LoginCommand("alice@example.com", "");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(LoginCommand.Password)), Is.True);
    }
}
