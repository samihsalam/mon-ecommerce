namespace MonEcommerce.Application.Catalogue.Models;

public record StockMovementDto(
    Guid Id,
    int PreviousQuantity,
    int NewQuantity,
    string Reason,
    string? AdminUserId,
    DateTimeOffset Timestamp);
