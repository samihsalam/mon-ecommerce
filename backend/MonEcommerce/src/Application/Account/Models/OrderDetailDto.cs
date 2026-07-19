namespace MonEcommerce.Application.Account.Models;

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    DateTimeOffset Date,
    int TotalInCents,
    string Status,
    string? TrackingNumber,
    AddressDto ShippingAddress,
    List<OrderItemDto> Items);
