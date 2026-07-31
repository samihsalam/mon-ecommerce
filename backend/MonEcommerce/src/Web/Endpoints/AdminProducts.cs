using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Catalogue.Commands;

namespace MonEcommerce.Web.Endpoints;

public class AdminProducts : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/products";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is each command's own [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour (same split as AdminOrders.cs/AdminReturns.cs).
        groupBuilder.MapPost(Create, "").RequireAuthorization();
        groupBuilder.MapPut(Update, "{id:guid}").RequireAuthorization();
        groupBuilder.MapDelete(Delete, "{id:guid}").RequireAuthorization();
    }

    [EndpointSummary("Create a product (admin only) — created unpublished")]
    public static async Task<IResult> Create([FromBody] CreateProductRequest request, ISender sender)
    {
        var id = await sender.Send(new CreateProductCommand(
            request.Name,
            request.Description,
            request.PriceInCents,
            request.CategoryId,
            request.Material,
            request.Color,
            request.Dimensions,
            request.InitialStock));

        return Results.Created($"/api/v1/admin/products/{id}", new { id });
    }

    [EndpointSummary("Update a product's fields (admin only) — invalidates the catalogue cache")]
    public static async Task<IResult> Update(Guid id, [FromBody] UpdateProductRequest request, ISender sender)
    {
        var product = await sender.Send(new UpdateProductCommand(
            id,
            request.Name,
            request.Description,
            request.PriceInCents,
            request.CategoryId,
            request.Material,
            request.Color,
            request.Dimensions));

        return Results.Ok(product);
    }

    [EndpointSummary("Soft-delete a product (admin only) — hides it from the catalogue, data preserved")]
    public static async Task<IResult> Delete(Guid id, ISender sender)
    {
        await sender.Send(new DeleteProductCommand(id));
        return Results.NoContent();
    }
}

public record CreateProductRequest(
    string Name,
    string Description,
    int PriceInCents,
    Guid CategoryId,
    string? Material,
    string? Color,
    string? Dimensions,
    int InitialStock);

public record UpdateProductRequest(
    string Name,
    string Description,
    int PriceInCents,
    Guid CategoryId,
    string? Material,
    string? Color,
    string? Dimensions);
