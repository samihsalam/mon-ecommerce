using MediatR;
using MonEcommerce.Application.Auth.Commands;
using Microsoft.AspNetCore.Mvc;

namespace MonEcommerce.Web.Endpoints;

public class Auth : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/auth";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(Register, "register").AllowAnonymous().RequireRateLimiting("auth");
        groupBuilder.MapPost(Login, "login").AllowAnonymous().RequireRateLimiting("auth");
        groupBuilder.MapPost(Refresh, "refresh").AllowAnonymous().RequireRateLimiting("auth");
        groupBuilder.MapPost(Logout, "logout").RequireAuthorization();
    }

    [EndpointSummary("Register a new account")]
    public static async Task<IResult> Register([FromBody] RegisterCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Succeeded ? Results.Ok(result.Value) : Results.BadRequest(new { result.Errors });
    }

    // result.Errors is currently always a safe, generic message ("Email ou mot de passe
    // incorrect.") — if AuthService.LoginAsync/RefreshTokenAsync ever start returning more
    // specific failure detail (e.g. lockout state), revisit whether it's still safe to
    // expose to an unauthenticated caller before adding it.
    [EndpointSummary("Login")]
    public static async Task<IResult> Login([FromBody] LoginCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : Results.Json(new { result.Errors }, statusCode: StatusCodes.Status401Unauthorized);
    }

    [EndpointSummary("Refresh access token")]
    public static async Task<IResult> Refresh([FromBody] RefreshTokenCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Succeeded
            ? Results.Ok(result.Value)
            : Results.Json(new { result.Errors }, statusCode: StatusCodes.Status401Unauthorized);
    }

    [EndpointSummary("Logout")]
    public static async Task<IResult> Logout([FromBody] LogoutCommand command, ISender sender)
    {
        await sender.Send(command);
        return Results.Ok();
    }
}
