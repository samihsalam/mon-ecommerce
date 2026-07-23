using MediatR;
using MonEcommerce.Application.Carts.Commands;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Carts.Queries;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Web.Endpoints;

public class Carts : IEndpointGroup
{
    // Not a cookie — see Story 4.1's Dev Notes (the Development CORS policy's AllowAnyOrigin() is
    // incompatible with credentialed cross-origin cookies). The client (Story 4.2) generates and
    // persists this id itself and sends it on every cart request; if a request arrives with
    // neither an authenticated user nor this header, one is generated here and returned via the
    // same header name so the caller always has an id to persist going forward.
    public const string SessionHeaderName = "X-Cart-Session-Id";

    public static string? RoutePrefix => "/api/v1/cart";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetCart).AllowAnonymous();
        groupBuilder.MapPost("/items", AddItem).AllowAnonymous();
        groupBuilder.MapPatch("/items/{itemId:guid}", UpdateItem).AllowAnonymous();
        groupBuilder.MapDelete("/items/{itemId:guid}", RemoveItem).AllowAnonymous();
    }

    [EndpointSummary("Get the current cart (anonymous via X-Cart-Session-Id, or the authenticated user's)")]
    public static async Task<IResult> GetCart(ISender sender, IUser currentUser, HttpContext httpContext)
    {
        var owner = ResolveOwner(currentUser, httpContext);
        var result = await sender.Send(new GetCartQuery(owner));
        return Results.Ok(result);
    }

    [EndpointSummary("Add a product to the cart, or increase its quantity if already present")]
    public static async Task<IResult> AddItem(ISender sender, IUser currentUser, HttpContext httpContext, AddCartItemRequest request)
    {
        var owner = ResolveOwner(currentUser, httpContext);
        var result = await sender.Send(new AddCartItemCommand(owner, request.ProductId, request.Quantity));
        return Results.Ok(result);
    }

    [EndpointSummary("Update a cart item's quantity — 0 removes it")]
    public static async Task<IResult> UpdateItem(
        ISender sender,
        IUser currentUser,
        HttpContext httpContext,
        Guid itemId,
        UpdateCartItemRequest request)
    {
        var owner = ResolveOwner(currentUser, httpContext);
        var result = await sender.Send(new UpdateCartItemCommand(owner, itemId, request.Quantity));
        return Results.Ok(result);
    }

    [EndpointSummary("Remove a cart item")]
    public static async Task<IResult> RemoveItem(ISender sender, IUser currentUser, HttpContext httpContext, Guid itemId)
    {
        var owner = ResolveOwner(currentUser, httpContext);
        var result = await sender.Send(new RemoveCartItemCommand(owner, itemId));
        return Results.Ok(result);
    }

    private static CartOwner ResolveOwner(IUser currentUser, HttpContext httpContext)
    {
        // Authenticated takes priority even if a (now-stale) session header is also present —
        // Login already merges the anonymous cart into the account cart, so continuing to honor
        // the old session id here would just silently resurrect an already-merged-away cart.
        if (currentUser.Id != null)
        {
            return CartOwner.ForUser(currentUser.Id);
        }

        if (httpContext.Request.Headers.TryGetValue(SessionHeaderName, out var existing) && !string.IsNullOrWhiteSpace(existing))
        {
            return CartOwner.ForSession(existing.ToString());
        }

        var generated = Guid.NewGuid().ToString();
        httpContext.Response.Headers[SessionHeaderName] = generated;
        return CartOwner.ForSession(generated);
    }
}

public record AddCartItemRequest(Guid ProductId, int Quantity);

public record UpdateCartItemRequest(int Quantity);
