namespace MonEcommerce.Application.Account.Models;

public record ReturnSummaryDto(Guid Id, string Status, string Reason, DateTimeOffset Created);
