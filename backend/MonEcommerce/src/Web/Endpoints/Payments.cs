using MediatR;
using MonEcommerce.Application.Payments.Commands;
using MonEcommerce.Application.Payments.Webhooks;

namespace MonEcommerce.Web.Endpoints;

public class Payments : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/payments";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(CreateIntent, "/create-intent").RequireAuthorization();
        // No .RequireAuthorization() — Stripe calls this directly with no user session; the
        // payload's signature (verified inside the handler) is the only security boundary.
        groupBuilder.MapPost(Webhook, "/webhook").AllowAnonymous();
    }

    [EndpointSummary("Create a Stripe PaymentIntent for the current cart + selected shipping option")]
    public static async Task<IResult> CreateIntent(ISender sender, CreatePaymentIntentRequest request)
    {
        var command = new CreatePaymentIntentCommand(
            request.ShippingOptionId,
            request.Street,
            request.City,
            request.PostalCode,
            request.Country);
        var result = await sender.Send(command);
        return Results.Ok(result);
    }

    [EndpointSummary("Stripe webhook — payment_intent.succeeded triggers order confirmation")]
    public static async Task<IResult> Webhook(ISender sender, HttpContext httpContext)
    {
        // The raw body, byte-for-byte, is required for Stripe signature verification — do not let
        // model binding parse this as JSON first.
        using var reader = new StreamReader(httpContext.Request.Body);
        var payload = await reader.ReadToEndAsync();
        var signatureHeader = httpContext.Request.Headers["Stripe-Signature"].ToString();

        await sender.Send(new HandleStripeWebhookCommand(payload, signatureHeader));
        return Results.Ok();
    }
}

public record CreatePaymentIntentRequest(string ShippingOptionId, string Street, string City, string PostalCode, string Country);
