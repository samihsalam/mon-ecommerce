namespace MonEcommerce.Application.Orders.Models;

public record AdminOrderSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    DateTimeOffset Date,
    int TotalInCents,
    string Status);
