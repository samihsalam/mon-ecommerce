using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Orders.Models;

public record AdminOrderFilter(
    OrderStatus? Status,
    DateTimeOffset? DateFrom,
    DateTimeOffset? DateTo,
    string? Search,
    int PageNumber,
    int PageSize);
