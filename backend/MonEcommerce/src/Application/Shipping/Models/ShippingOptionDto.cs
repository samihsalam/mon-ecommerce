namespace MonEcommerce.Application.Shipping.Models;

public record ShippingOptionDto(string Id, string Name, int PriceInCents, string EstimatedDelay);
