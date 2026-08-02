using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Domain.Enums;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.Commands;

public class UpdateReturnStatusCommandValidatorTests
{
    private UpdateReturnStatusCommandValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new UpdateReturnStatusCommandValidator();

    [Test]
    public void ShouldBeValidWhenApprovingWithoutAReason()
    {
        var result = _validator.Validate(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Approved));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenRejectingWithoutAReason()
    {
        var result = _validator.Validate(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Rejected));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void ShouldBeValidWhenRejectingWithAReason()
    {
        var result = _validator.Validate(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Rejected, "Produit porté."));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailForPendingOrRefundedStatuses()
    {
        Assert.That(_validator.Validate(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Pending)).IsValid, Is.False);
        Assert.That(_validator.Validate(new UpdateReturnStatusCommand(Guid.NewGuid(), ReturnStatus.Refunded)).IsValid, Is.False);
    }
}
