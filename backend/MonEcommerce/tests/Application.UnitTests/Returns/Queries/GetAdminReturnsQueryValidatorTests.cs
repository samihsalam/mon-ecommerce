using MonEcommerce.Application.Returns.Queries;
using MonEcommerce.Domain.Enums;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Returns.Queries;

public class GetAdminReturnsQueryValidatorTests
{
    private GetAdminReturnsQueryValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new GetAdminReturnsQueryValidator();

    [Test]
    public void ShouldBeValidWithAllFiltersOmitted()
    {
        var result = _validator.Validate(new GetAdminReturnsQuery(null, null, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new GetAdminReturnsQuery(ReturnStatus.Pending, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenDateToIsBeforeDateFrom()
    {
        var result = _validator.Validate(new GetAdminReturnsQuery(null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.That(result.IsValid, Is.False);
    }
}
