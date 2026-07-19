namespace MonEcommerce.Application.Account.Models;

public record OrderItemDto(string ProductName, int UnitPriceInCents, int Quantity);
