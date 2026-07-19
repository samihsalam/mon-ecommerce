using MediatR;
using MonEcommerce.Application.Account.Commands;
using MonEcommerce.Application.Account.Queries;
using Microsoft.AspNetCore.Mvc;

namespace MonEcommerce.Web.Endpoints;

public class Account : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/account";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetProfile, "profile").RequireAuthorization();
        groupBuilder.MapPatch(UpdateProfile, "profile").RequireAuthorization();
        groupBuilder.MapGet(GetOrders, "orders").RequireAuthorization();
        groupBuilder.MapGet(GetOrderDetail, "orders/{orderId:guid}").RequireAuthorization();
    }

    [EndpointSummary("Get the current user's profile")]
    public static async Task<IResult> GetProfile(ISender sender)
    {
        var profile = await sender.Send(new GetProfileQuery());
        return Results.Ok(profile);
    }

    [EndpointSummary("Update the current user's profile")]
    public static async Task<IResult> UpdateProfile([FromBody] UpdateProfileCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { result.Errors });
    }

    [EndpointSummary("Get the current user's paginated order history")]
    public static async Task<IResult> GetOrders(ISender sender, int page = 1, int pageSize = 10)
    {
        var orders = await sender.Send(new GetOrdersQuery(page, pageSize));
        return Results.Ok(orders);
    }

    [EndpointSummary("Get full detail for one of the current user's orders")]
    public static async Task<IResult> GetOrderDetail(Guid orderId, ISender sender)
    {
        var order = await sender.Send(new GetOrderDetailQuery(orderId));
        return Results.Ok(order);
    }
}
