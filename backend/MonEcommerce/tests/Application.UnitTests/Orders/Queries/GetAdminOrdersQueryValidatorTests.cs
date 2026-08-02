using MonEcommerce.Application.Orders.Queries;
using MonEcommerce.Domain.Enums;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Orders.Queries;

public class GetAdminOrdersQueryValidatorTests
{
    private GetAdminOrdersQueryValidator _validator = null!;

    [SetUp]
    public void Setup() => _validator = new GetAdminOrdersQueryValidator();

    [Test]
    public void ShouldBeValidForAWellFormedRequest()
    {
        var result = _validator.Validate(new GetAdminOrdersQuery(OrderStatus.Shipped, DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow, "Salma", 1, 20));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldBeValidWithAllFiltersOmitted()
    {
        var result = _validator.Validate(new GetAdminOrdersQuery(null, null, null, null));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ShouldFailWhenPageNumberIsZeroOrNegative()
    {
        Assert.That(_validator.Validate(new GetAdminOrdersQuery(null, null, null, null, PageNumber: 0)).IsValid, Is.False);
        Assert.That(_validator.Validate(new GetAdminOrdersQuery(null, null, null, null, PageNumber: -1)).IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenPageSizeIsZeroOrNegative()
    {
        Assert.That(_validator.Validate(new GetAdminOrdersQuery(null, null, null, null, PageSize: 0)).IsValid, Is.False);
    }

    [Test]
    public void ShouldFailWhenDateToIsBeforeDateFrom()
    {
        var result = _validator.Validate(new GetAdminOrdersQuery(
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(-1),
            null));

        Assert.That(result.IsValid, Is.False);
    }
}
