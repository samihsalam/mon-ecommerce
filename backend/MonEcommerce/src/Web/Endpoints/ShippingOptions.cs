using MediatR;
using MonEcommerce.Application.Shipping.Queries;

namespace MonEcommerce.Web.Endpoints;

public class ShippingOptions : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/shipping-options";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetShippingOptions).AllowAnonymous();
    }

    [EndpointSummary("List available shipping options (name, price, estimated delay)")]
    public static async Task<IResult> GetShippingOptions(ISender sender)
    {
        var options = await sender.Send(new GetShippingOptionsQuery());
        return Results.Ok(options);
    }
}
