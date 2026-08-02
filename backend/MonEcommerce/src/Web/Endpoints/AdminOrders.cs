using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Orders.Commands;
using MonEcommerce.Application.Orders.Queries;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Web.Endpoints;

public class AdminOrders : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/orders";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the actual
        // admin-role gate is each command/query's own [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour (same split already used everywhere else in this
        // codebase between HTTP-level auth and MediatR-pipeline authorization).
        groupBuilder.MapGet(GetAdminOrders, "").RequireAuthorization();
        groupBuilder.MapPatch(UpdateOrderStatus, "{orderId:guid}/status").RequireAuthorization();
    }

    // Story 7.1. Named GetAdminOrders, not GetOrders — Web/Endpoints/Account.cs already has a
    // customer-facing GetOrders; a same-named handler in a different IEndpointGroup collides
    // (endpoint names, inferred from the method name for method-group handlers, must be globally
    // unique — the exact bug class just fixed live while running the app).
    [EndpointSummary("List and filter all orders (admin only) — paginated, sorted by date descending")]
    public static async Task<IResult> GetAdminOrders(
        ISender sender,
        OrderStatus? status = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var result = await sender.Send(new GetAdminOrdersQuery(status, dateFrom, dateTo, search, pageNumber, pageSize));
        return Results.Ok(result);
    }

    // Method name unique across every endpoint group — ASP.NET Core Minimal APIs infers each
    // endpoint's Name from the handler method name when mapped as a method group, and endpoint
    // names must be globally unique. This collided with AdminReturns.UpdateStatus (pre-existing
    // since Story 5.3) — only ever caught by actually running the app, not by `dotnet build`/
    // `test`, which never construct the real ASP.NET Core routing table.
    [EndpointSummary("Update an order's status (admin only) — triggers shipment/delivery notification emails")]
    public static async Task<IResult> UpdateOrderStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request, ISender sender)
    {
        await sender.Send(new UpdateOrderStatusCommand(orderId, request.NewStatus, request.TrackingNumber));
        return Results.NoContent();
    }
}

public record UpdateOrderStatusRequest(OrderStatus NewStatus, string? TrackingNumber);
