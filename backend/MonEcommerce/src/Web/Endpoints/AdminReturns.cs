using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Application.Returns.Queries;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Web.Endpoints;

public class AdminReturns : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/returns";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is each command/query's own [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour (same split as Story 5.2's AdminOrders.cs).
        groupBuilder.MapGet(GetAdminReturns, "").RequireAuthorization();
        groupBuilder.MapPatch(UpdateReturnStatus, "{returnId:guid}").RequireAuthorization();
        groupBuilder.MapPost(IssueRefund, "{returnId:guid}/refund").RequireAuthorization();
    }

    // Story 7.3. Method name unique across every endpoint group — same lesson as
    // AdminOrders.GetAdminOrders (not GetOrders).
    [EndpointSummary("List and filter all return requests (admin only)")]
    public static async Task<IResult> GetAdminReturns(
        ISender sender,
        ReturnStatus? status = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null)
    {
        var result = await sender.Send(new GetAdminReturnsQuery(status, dateFrom, dateTo));
        return Results.Ok(result);
    }

    // Method name unique across every endpoint group — see AdminOrders.cs's UpdateOrderStatus
    // comment (this collided with it).
    [EndpointSummary("Validate or reject a return request (admin only) — notifies the customer")]
    public static async Task<IResult> UpdateReturnStatus(Guid returnId, [FromBody] UpdateReturnStatusRequest request, ISender sender)
    {
        await sender.Send(new UpdateReturnStatusCommand(returnId, request.NewStatus, request.Reason));
        return Results.NoContent();
    }

    [EndpointSummary("Issue a Stripe refund for a validated return (admin only)")]
    public static async Task<IResult> IssueRefund(Guid returnId, ISender sender)
    {
        await sender.Send(new IssueReturnRefundCommand(returnId));
        return Results.NoContent();
    }
}

public record UpdateReturnStatusRequest(ReturnStatus NewStatus, string? Reason = null);
