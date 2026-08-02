namespace MonEcommerce.Application.Returns.Models;

public record AdminReturnSummaryDto(
    Guid Id,
    string OrderNumber,
    string CustomerName,
    string Reason,
    DateTimeOffset Date,
    string Status,
    List<string> PhotoUrls);
