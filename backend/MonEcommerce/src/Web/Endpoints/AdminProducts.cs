using MediatR;
using Microsoft.AspNetCore.Mvc;
using MonEcommerce.Application.Catalogue.Commands;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Catalogue.Queries;

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

        // Story 6.2: image management — nested resources under the same product, not a separate
        // endpoint-group class.
        groupBuilder.MapPost(AddImage, "{id:guid}/images").RequireAuthorization();
        groupBuilder.MapPatch(ReorderImages, "{id:guid}/images/order").RequireAuthorization();
        groupBuilder.MapDelete(DeleteImage, "{id:guid}/images/{imageId:guid}").RequireAuthorization();

        // Story 6.3: bulk CSV import. Literal "import" segments never collide with the {id:guid}
        // routes above — "import" fails the :guid constraint, so routing falls through correctly.
        groupBuilder.MapPost(ImportCsv, "import").RequireAuthorization();
        groupBuilder.MapGet(DownloadImportTemplate, "import/template").RequireAuthorization();

        // Story 6.4: stock management.
        groupBuilder.MapPatch(UpdateStock, "{id:guid}/stock").RequireAuthorization();
        groupBuilder.MapGet(GetStockHistory, "{id:guid}/stock-history").RequireAuthorization();

        // Story 6.5: publish/unpublish — the one endpoint that sets IsPublished (see Update's
        // summary and Story 6.1's Dev Notes for why it's excluded from PUT).
        groupBuilder.MapPatch(Publish, "{id:guid}/publish").RequireAuthorization();
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

    // multipart/form-data — same IFormFile -> Application-layer-record conversion pattern as
    // Account.CreateReturnRequest (Story 5.1's first file-upload endpoint).
    [EndpointSummary("Upload a product image via Cloudinary (admin only) — WebP, 3:4 crop, max width 1200px")]
    public static async Task<IResult> AddImage(Guid id, IFormFile file, ISender sender)
    {
        var upload = new ProductImageUpload(file.OpenReadStream(), file.FileName);
        var image = await sender.Send(new AddProductImageCommand(id, upload));
        return Results.Created($"/api/v1/admin/products/{id}/images/{image.Id}", image);
    }

    [EndpointSummary("Reorder a product's images (admin only)")]
    public static async Task<IResult> ReorderImages(Guid id, [FromBody] ReorderProductImagesRequest request, ISender sender)
    {
        await sender.Send(new ReorderProductImagesCommand(id, request.ImageIds));
        return Results.NoContent();
    }

    [EndpointSummary("Delete a product image (admin only) — removes it from Cloudinary and the product's gallery")]
    public static async Task<IResult> DeleteImage(Guid id, Guid imageId, ISender sender)
    {
        await sender.Send(new DeleteProductImageCommand(id, imageId));
        return Results.NoContent();
    }

    // multipart/form-data — same IFormFile -> Application-layer Stream conversion pattern as
    // AddImage/Account.CreateReturnRequest.
    [EndpointSummary("Bulk-import products from a CSV file (admin only) — invalid/duplicate rows are reported, not fatal")]
    public static async Task<IResult> ImportCsv(IFormFile file, ISender sender)
    {
        var result = await sender.Send(new ImportProductsCsvCommand(file.OpenReadStream()));
        return Results.Ok(result);
    }

    [EndpointSummary("Download the CSV import template (admin only)")]
    public static IResult DownloadImportTemplate()
    {
        const string template = "nom,description,prix,catégorie,matière,couleur,stock\n";
        return Results.File(System.Text.Encoding.UTF8.GetBytes(template), "text/csv", "modele-import-produits.csv");
    }

    [EndpointSummary("Set a product's stock quantity and alert threshold (admin only) — logs a stock movement")]
    public static async Task<IResult> UpdateStock(Guid id, [FromBody] UpdateStockRequest request, ISender sender)
    {
        var stock = await sender.Send(new UpdateStockCommand(id, request.Quantity, request.AlertThreshold, request.Reason));
        return Results.Ok(stock);
    }

    [EndpointSummary("Get a product's stock movement history (admin only)")]
    public static async Task<IResult> GetStockHistory(Guid id, ISender sender)
    {
        var history = await sender.Send(new GetStockHistoryQuery(id));
        return Results.Ok(history);
    }

    [EndpointSummary("Publish or unpublish a product (admin only) — publishing requires at least one image")]
    public static async Task<IResult> Publish(Guid id, [FromBody] PublishProductRequest request, ISender sender)
    {
        var product = await sender.Send(new PublishProductCommand(id, request.IsPublished));
        return Results.Ok(product);
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

public record ReorderProductImagesRequest(IReadOnlyList<Guid> ImageIds);

public record UpdateStockRequest(int Quantity, int AlertThreshold, string? Reason);

public record PublishProductRequest(bool IsPublished);
