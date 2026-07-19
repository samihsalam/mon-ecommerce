using MonEcommerce.Application.Auth.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Commands;

public class ResetPasswordCommandValidatorTests
{
    private ResetPasswordCommandValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new ResetPasswordCommandValidator();
    }

    [Test]
    public void ShouldBeValidWhenAllFieldsAreCorrect()
    {
        var command = new ResetPasswordCommand("alice@example.com", "some-token", "newpassword123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsInvalid()
    {
        var command = new ResetPasswordCommand("not-an-email", "some-token", "newpassword123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(ResetPasswordCommand.Email)), Is.True);
    }

    [Test]
    public void ShouldFailWhenTokenIsEmpty()
    {
        var command = new ResetPasswordCommand("alice@example.com", "", "newpassword123");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(ResetPasswordCommand.Token)), Is.True);
    }

    [Test]
    public void ShouldFailWhenNewPasswordIsTooShort()
    {
        var command = new ResetPasswordCommand("alice@example.com", "some-token", "short");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(ResetPasswordCommand.NewPassword)), Is.True);
    }
}
