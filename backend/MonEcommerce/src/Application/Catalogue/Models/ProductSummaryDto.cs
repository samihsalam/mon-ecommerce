namespace MonEcommerce.Application.Catalogue.Models;

public record ProductSummaryDto(
    Guid Id,
    string Name,
    int PriceInCents,
    string? Material,
    string? Color,
    string? ImageUrl,
    Guid CategoryId,
    string CategoryName,
    bool InStock);
