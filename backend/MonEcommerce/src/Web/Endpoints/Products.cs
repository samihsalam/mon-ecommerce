using MediatR;
using MonEcommerce.Application.Catalogue.Queries;

namespace MonEcommerce.Web.Endpoints;

public class Products : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/products";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetProducts).AllowAnonymous();
    }

    [EndpointSummary("Browse the product catalogue with optional filters")]
    public static async Task<IResult> GetProducts(
        ISender sender,
        Guid? categoryId = null,
        string? material = null,
        string? color = null,
        int? priceMin = null,
        int? priceMax = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var result = await sender.Send(new GetProductsQuery(categoryId, material, color, priceMin, priceMax, pageNumber, pageSize));
        return Results.Ok(result);
    }
}
