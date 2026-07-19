namespace MonEcommerce.Application.Account.Models;

public record OrderSummaryDto(Guid Id, string OrderNumber, DateTimeOffset Date, int TotalInCents, string Status);
