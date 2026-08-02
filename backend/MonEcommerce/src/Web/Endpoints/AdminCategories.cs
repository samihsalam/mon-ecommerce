using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Catalogue.Commands;

namespace MonEcommerce.Web.Endpoints;

// Categories are a distinct resource from products — unlike Story 6.2/6.3/6.4's nested
// /products/{id}/... routes, this is its own top-level endpoint group.
public class AdminCategories : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/admin/categories";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        // .RequireAuthorization() only proves the caller is authenticated as someone — the real
        // admin-role gate is each command's own [Authorize(Roles = Roles.Administrator)],
        // enforced by AuthorizationBehaviour (same split as AdminOrders.cs/AdminProducts.cs).
        groupBuilder.MapPost(Create, "").RequireAuthorization();
        groupBuilder.MapDelete(Delete, "{id:guid}").RequireAuthorization();
    }

    [EndpointSummary("Create a category or subcategory (admin only) — auto-generates a kebab-case slug")]
    public static async Task<IResult> Create([FromBody] CreateCategoryRequest request, ISender sender)
    {
        var category = await sender.Send(new CreateCategoryCommand(request.Name, request.ParentId));
        return Results.Created($"/api/v1/admin/categories/{category.Id}", category);
    }

    [EndpointSummary("Delete a category (admin only) — blocked if it has children or any products")]
    public static async Task<IResult> Delete(Guid id, ISender sender)
    {
        await sender.Send(new DeleteCategoryCommand(id));
        return Results.NoContent();
    }
}

public record CreateCategoryRequest(string Name, Guid? ParentId);
