namespace MonEcommerce.Application.Carts.Models;

public record CartItemDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string? ImageUrl,
    int UnitPriceInCents,
    int Quantity,
    int LineTotalInCents);
