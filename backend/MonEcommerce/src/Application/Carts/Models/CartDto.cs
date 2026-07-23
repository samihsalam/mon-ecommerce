namespace MonEcommerce.Application.Carts.Models;

public record CartDto(List<CartItemDto> Items, int TotalInCents);
