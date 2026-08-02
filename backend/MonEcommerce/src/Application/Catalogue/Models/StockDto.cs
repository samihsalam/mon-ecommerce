namespace MonEcommerce.Application.Catalogue.Models;

public record StockDto(Guid ProductId, int Quantity, int AlertThreshold);
