using MediatR;
using MonEcommerce.Application.Catalogue.Queries;

namespace MonEcommerce.Web.Endpoints;

public class AdminAnalytics : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/analytics";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is each query's own [Authorize(Roles = Roles.Administrator)], enforced
        // by AuthorizationBehaviour (same split as every other admin endpoint group).
        groupBuilder.MapGet(GetTopProducts, "/top-products").RequireAuthorization();
        groupBuilder.MapGet(GetLowStockProducts, "/low-stock").RequireAuthorization();
    }

    [EndpointSummary("Top 10 most-viewed and top 10 best-selling products over the last 7 days (admin only) — cached 1 hour")]
    public static async Task<IResult> GetTopProducts(ISender sender)
    {
        var result = await sender.Send(new GetTopProductsQuery());
        return Results.Ok(result);
    }

    [EndpointSummary("Products at or below their stock alert threshold (admin only) — never cached")]
    public static async Task<IResult> GetLowStockProducts(ISender sender)
    {
        var result = await sender.Send(new GetLowStockProductsQuery());
        return Results.Ok(result);
    }
}
