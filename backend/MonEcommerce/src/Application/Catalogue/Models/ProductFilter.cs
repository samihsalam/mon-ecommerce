namespace MonEcommerce.Application.Catalogue.Models;

public record ProductFilter(Guid? CategoryId, string? Material, string? Color, int? PriceMin, int? PriceMax, string? Search, int PageNumber, int PageSize);
