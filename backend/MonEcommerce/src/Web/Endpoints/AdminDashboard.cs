using MediatR;
using MonEcommerce.Application.Dashboard.Queries;

namespace MonEcommerce.Web.Endpoints;

// The dashboard is its own resource, not nested under products/orders/returns/categories.
public class AdminDashboard : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/dashboard";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is the query's own [Authorize(Roles = Roles.Administrator)], enforced
        // by AuthorizationBehaviour (same split as every other admin endpoint group).
        groupBuilder.MapGet(GetDashboardMetrics, "").RequireAuthorization();
    }

    [EndpointSummary("Get today's key shop metrics (admin only) — revenue, orders count, average order value")]
    public static async Task<IResult> GetDashboardMetrics(ISender sender)
    {
        var metrics = await sender.Send(new GetDashboardMetricsQuery());
        return Results.Ok(metrics);
    }
}
