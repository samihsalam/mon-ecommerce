using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Orders.Commands;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Web.Endpoints;

public class AdminOrders : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/orders";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the actual
        // admin-role gate is UpdateOrderStatusCommand's [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour (same split already used everywhere else in this
        // codebase between HTTP-level auth and MediatR-pipeline authorization).
        groupBuilder.MapPatch(UpdateStatus, "{orderId:guid}/status").RequireAuthorization();
    }

    [EndpointSummary("Update an order's status (admin only) — triggers shipment/delivery notification emails")]
    public static async Task<IResult> UpdateStatus(Guid orderId, [FromBody] UpdateOrderStatusRequest request, ISender sender)
    {
        await sender.Send(new UpdateOrderStatusCommand(orderId, request.NewStatus, request.TrackingNumber));
        return Results.NoContent();
    }
}

public record UpdateOrderStatusRequest(OrderStatus NewStatus, string? TrackingNumber);
