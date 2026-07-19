using MonEcommerce.Application.Account.Queries;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Account.Queries;

public class GetOrdersQueryValidatorTests
{
    private GetOrdersQueryValidator _validator = null!;

    [SetUp]
    public void Setup()
    {
        _validator = new GetOrdersQueryValidator();
    }

    [Test]
    public void ShouldBeValidWithDefaultValues()
    {
        var result = _validator.Validate(new GetOrdersQuery());

        Assert.That(result.IsValid, Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void ShouldFailWhenPageIsNotPositive(int page)
    {
        var result = _validator.Validate(new GetOrdersQuery(page, 10));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(GetOrdersQuery.Page)), Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(101)]
    public void ShouldFailWhenPageSizeIsOutOfRange(int pageSize)
    {
        var result = _validator.Validate(new GetOrdersQuery(1, pageSize));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Exists(e => e.PropertyName == nameof(GetOrdersQuery.PageSize)), Is.True);
    }

    [Test]
    public void ShouldBeValidAtTheBoundaries()
    {
        var result = _validator.Validate(new GetOrdersQuery(1, 100));

        Assert.That(result.IsValid, Is.True);
    }
}
