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
}
