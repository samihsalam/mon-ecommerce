using MediatR;
using MonEcommerce.Application.Payments.Commands;

namespace MonEcommerce.Web.Endpoints;

public class Payments : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/payments";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapPost(CreateIntent, "/create-intent").RequireAuthorization();
    }

    [EndpointSummary("Create a Stripe PaymentIntent for the current cart + selected shipping option")]
    public static async Task<IResult> CreateIntent(ISender sender, CreatePaymentIntentRequest request)
    {
        var result = await sender.Send(new CreatePaymentIntentCommand(request.ShippingOptionId));
        return Results.Ok(result);
    }
}

public record CreatePaymentIntentRequest(string ShippingOptionId);
