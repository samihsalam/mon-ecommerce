namespace MonEcommerce.Application.Catalogue.Models;

// The admin-facing product shape — unlike ProductDetailDto (public catalogue, published-only),
// this includes IsPublished and raw StockQuantity since an admin needs to see both regardless of
// publication state.
public record AdminProductDto(
    Guid Id,
    string Name,
    string Description,
    int PriceInCents,
    string? Material,
    string? Color,
    string? Dimensions,
    Guid CategoryId,
    bool IsPublished,
    int StockQuantity);
