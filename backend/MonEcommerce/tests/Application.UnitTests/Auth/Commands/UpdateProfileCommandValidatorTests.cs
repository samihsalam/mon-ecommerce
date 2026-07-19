using MonEcommerce.Application.Account.Commands;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Auth.Commands;

public class UpdateProfileCommandValidatorTests
{
    private UpdateProfileCommandValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new UpdateProfileCommandValidator();
    }

    [Test]
    public void ShouldBeValidWhenNameAndEmailAreCorrect()
    {
        var command = new UpdateProfileCommand("Alice", "alice@example.com", null);

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenNameIsEmpty()
    {
        var command = new UpdateProfileCommand("", "alice@example.com", null);

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(UpdateProfileCommand.Name)), Is.True);
    }

    [Test]
    public void ShouldFailWhenEmailIsInvalid()
    {
        var command = new UpdateProfileCommand("Alice", "not-an-email", null);

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(UpdateProfileCommand.Email)), Is.True);
    }
}
