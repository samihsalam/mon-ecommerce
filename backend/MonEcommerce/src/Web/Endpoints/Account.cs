using MediatR;
using MonEcommerce.Application.Account.Commands;
using MonEcommerce.Application.Account.Queries;
using MonEcommerce.Application.Returns.Commands;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Domain.Enums;
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
        groupBuilder.MapGet(GetOrderByPaymentIntent, "orders/by-payment-intent/{paymentIntentId}").RequireAuthorization();
        groupBuilder.MapPost(CreateReturnRequest, "orders/{orderId:guid}/returns")
            .RequireAuthorization()
            .DisableAntiforgery();
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

    // Polled by the checkout confirmation page (Story 4.6) — order creation happens
    // asynchronously via a Stripe webhook, so the browser only ever knows the payment intent id,
    // never the resulting Order.Id, until this resolves. 404 while still pending, 409 if stock
    // was insufficient and the payment was refunded instead (see GetOrderByPaymentIntentAsync).
    [EndpointSummary("Poll for the order resulting from a Stripe payment intent")]
    public static async Task<IResult> GetOrderByPaymentIntent(string paymentIntentId, ISender sender)
    {
        var order = await sender.Send(new GetOrderByPaymentIntentQuery(paymentIntentId));
        return Results.Ok(order);
    }

    // multipart/form-data — the first file-upload endpoint in this codebase (Story 5.1's AC #4
    // "optional photos"). IFormFile is converted to the Application layer's own ReturnPhotoUpload
    // (a plain Stream + filename) — the Application project has no reference to ASP.NET Core.
    [EndpointSummary("Request a return for one of the current user's delivered orders")]
    public static async Task<IResult> CreateReturnRequest(
        Guid orderId,
        [FromForm] ReturnReason reason,
        [FromForm] string description,
        IFormFileCollection? photos,
        ISender sender)
    {
        var uploads = (photos ?? Enumerable.Empty<IFormFile>())
            .Select(f => new ReturnPhotoUpload(f.OpenReadStream(), f.FileName))
            .ToList();

        var result = await sender.Send(new CreateReturnRequestCommand(orderId, reason, description, uploads));
        return Results.Ok(result);
    }
}
