namespace MonEcommerce.Application.Catalogue.Models;

public record ProductDetailDto(
    Guid Id,
    string Name,
    string Description,
    int PriceInCents,
    string? Material,
    string? Color,
    string? Dimensions,
    int StockQuantity,
    bool InStock,
    Guid CategoryId,
    string CategoryName,
    string CategorySlug,
    List<string> ImageUrls);
