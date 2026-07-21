using MediatR;
using MonEcommerce.Application.Catalogue.Queries;

namespace MonEcommerce.Web.Endpoints;

public class Products : IEndpointGroup
{
    public static string? RoutePrefix => "/api/v1/products";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.MapGet(GetProducts).AllowAnonymous();
        groupBuilder.MapGet("/suggestions", GetSuggestions).AllowAnonymous();
        groupBuilder.MapGet("/categories", GetCategories).AllowAnonymous();
        // The {id:guid} constraint means this can never ambiguously capture the literal
        // /suggestions or /categories routes above (and ASP.NET Core prefers literal segments
        // over parameterized ones regardless).
        groupBuilder.MapGet("/{id:guid}", GetProductById).AllowAnonymous();
    }

    [EndpointSummary("Browse the product catalogue with optional filters, including keyword search")]
    public static async Task<IResult> GetProducts(
        ISender sender,
        Guid? categoryId = null,
        string? material = null,
        string? color = null,
        int? priceMin = null,
        int? priceMax = null,
        string? search = null,
        int pageNumber = 1,
        int pageSize = 20)
    {
        var result = await sender.Send(new GetProductsQuery(categoryId, material, color, priceMin, priceMax, search, pageNumber, pageSize));
        return Results.Ok(result);
    }

    [EndpointSummary("Typeahead suggestions (categories + product names) for a partial search term")]
    public static async Task<IResult> GetSuggestions(ISender sender, string? search = null)
    {
        // search is nullable purely so an omitted query param reaches GetSearchSuggestionsQueryValidator
        // (422, this codebase's standard validation-failure shape) instead of failing ASP.NET Core's
        // model binding for a required non-nullable parameter (a bare 400).
        var result = await sender.Send(new GetSearchSuggestionsQuery(search));
        return Results.Ok(result);
    }

    [EndpointSummary("List all product categories — used to populate the search empty-state's suggested category links")]
    public static async Task<IResult> GetCategories(ISender sender)
    {
        var result = await sender.Send(new GetCategoriesQuery());
        return Results.Ok(result);
    }

    [EndpointSummary("Full product detail (description, dimensions, stock, images) for the product detail page")]
    public static async Task<IResult> GetProductById(ISender sender, Guid id)
    {
        var result = await sender.Send(new GetProductByIdQuery(id));
        return Results.Ok(result);
    }
}
