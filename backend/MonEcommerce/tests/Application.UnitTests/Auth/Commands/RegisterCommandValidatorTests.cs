using MonEcommerce.Application.Auth.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Commands;

public class RegisterCommandValidatorTests
{
    private RegisterCommandValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new RegisterCommandValidator();
    }

    [Test]
    public void ShouldBeValidWhenAllFieldsAreCorrect()
    {
        var command = new RegisterCommand("Alice", "alice@example.com", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenNameIsEmpty()
    {
        var command = new RegisterCommand("", "alice@example.com", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(RegisterCommand.Name)), Is.True);
    }

    [Test]
    public void ShouldFailWhenPasswordIsTooShort()
    {
        var command = new RegisterCommand("Alice", "alice@example.com", "short");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(RegisterCommand.Password)), Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsInvalid()
    {
        var command = new RegisterCommand("Alice", "not-an-email", "password123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(RegisterCommand.Email)), Is.True);
    }
}
