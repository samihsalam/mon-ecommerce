namespace MonEcommerce.Application.Catalogue.Models;

public record ProductFilter(Guid? CategoryId, string? Material, string? Color, int? PriceMin, int? PriceMax, int PageNumber, int PageSize);
