using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Domain.Enums;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.Commands;

public class CreateReturnRequestCommandValidatorTests
{
    private CreateReturnRequestCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new CreateReturnRequestCommandValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new CreateReturnRequestCommand(Guid.NewGuid(), ReturnReason.WrongSize, "Trop petit.", []));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenDescriptionIsEmpty()
    {
        var result = _validator.Validate(new CreateReturnRequestCommand(Guid.NewGuid(), ReturnReason.WrongSize, "", []));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenDescriptionExceedsMaxLength()
    {
        var result = _validator.Validate(new CreateReturnRequestCommand(Guid.NewGuid(), ReturnReason.WrongSize, new string('a', 2001), []));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenOrderIdIsEmpty()
    {
        var result = _validator.Validate(new CreateReturnRequestCommand(Guid.Empty, ReturnReason.WrongSize, "desc", []));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenReasonIsUndefined()
    {
        var result = _validator.Validate(new CreateReturnRequestCommand(Guid.NewGuid(), (ReturnReason)999, "desc", []));

        Assert.That(result.IsValid, Is.False);
    }
}
