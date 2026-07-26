using MonEcommerce.Application.Shipping.Queries;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Shipping.Queries;

public class GetShippingOptionsQueryHandlerTests
{
    [Test]
    public async Task Handle_ShouldReturnExactlyTwoOptions()
    {
        var result = await new GetShippingOptionsQueryHandler().Handle(new GetShippingOptionsQuery(), CancellationToken.None);

        Assert.That(result, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Handle_ShouldAlwaysIncludeStandardFirst()
    {
        var result = await new GetShippingOptionsQueryHandler().Handle(new GetShippingOptionsQuery(), CancellationToken.None);

        Assert.That(result[0].Id, Is.EqualTo("standard"));
    }

    [Test]
    public async Task Handle_ShouldReturnOptionsWithNonEmptyNamePositivePriceAndNonEmptyDelay()
    {
        var result = await new GetShippingOptionsQueryHandler().Handle(new GetShippingOptionsQuery(), CancellationToken.None);

        Assert.That(result, Is.All.Matches<Application.Shipping.Models.ShippingOptionDto>(o =>
            !string.IsNullOrWhiteSpace(o.Id) &&
            !string.IsNullOrWhiteSpace(o.Name) &&
            o.PriceInCents > 0 &&
            !string.IsNullOrWhiteSpace(o.EstimatedDelay)));
    }
}
