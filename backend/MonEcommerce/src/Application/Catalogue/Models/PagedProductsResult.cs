namespace MonEcommerce.Application.Catalogue.Models;

public record PagedProductsResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages);
