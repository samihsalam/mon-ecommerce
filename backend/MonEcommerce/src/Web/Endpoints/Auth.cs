using MediatR;
using MonEcommerce.Application.Auth.Commands;
using MonEcommerce.Application.Common.Interfaces;
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
    public static async Task<IResult> Login(
        [FromBody] LoginCommand command,
        ISender sender,
        ICartService cartService,
        HttpContext httpContext,
        ILogger<Auth> logger)
    {
        var result = await sender.Send(command);
        if (!result.Succeeded)
        {
            return Results.Json(new { result.Errors }, statusCode: StatusCodes.Status401Unauthorized);
        }

        // Merge only after a successful login, and only when the client actually sent an
        // anonymous session id — MergeAnonymousCartAsync itself no-ops cleanly if that session
        // has no cart (or an already-expired one), so this is safe to call unconditionally
        // whenever the header is present.
        //
        // Wrapped in try/catch deliberately: by this point tokens are already issued and
        // persisted (IssueTokensAsync's own SaveChangesAsync already committed), so a merge
        // failure (transient DB error, etc.) must not turn an otherwise-successful login into an
        // unhandled 500 — the user gets their session either way; a failed merge just means their
        // anonymous cart's items didn't carry over, not that login itself failed.
        if (httpContext.Request.Headers.TryGetValue(Carts.SessionHeaderName, out var sessionId) && !string.IsNullOrWhiteSpace(sessionId))
        {
            try
            {
                await cartService.MergeAnonymousCartAsync(sessionId.ToString(), result.Value!.UserId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to merge anonymous cart {SessionId} into user {UserId} after login.", sessionId.ToString(), result.Value!.UserId);
            }
        }

        return Results.Ok(result.Value);
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
