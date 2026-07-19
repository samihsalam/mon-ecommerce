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
        groupBuilder.MapPost(ForgotPassword, "forgot-password").AllowAnonymous().RequireRateLimiting("auth");
        groupBuilder.MapPost(ResetPassword, "reset-password").AllowAnonymous().RequireRateLimiting("auth");
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

    // Always 200 regardless of whether the email is registered — ForgotPasswordAsync itself
    // never returns failure, by design (no email enumeration; see AuthService.cs).
    [EndpointSummary("Request a password reset email")]
    public static async Task<IResult> ForgotPassword([FromBody] ForgotPasswordCommand command, ISender sender)
    {
        await sender.Send(command);
        return Results.Ok();
    }

    [EndpointSummary("Reset password with a token")]
    public static async Task<IResult> ResetPassword([FromBody] ResetPasswordCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Succeeded
            ? Results.Ok()
            : Results.Json(new { result.Errors }, statusCode: StatusCodes.Status400BadRequest);
    }
}
