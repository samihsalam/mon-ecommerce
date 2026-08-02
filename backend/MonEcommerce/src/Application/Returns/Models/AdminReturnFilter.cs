using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Returns.Models;

public record AdminReturnFilter(ReturnStatus? Status, DateTimeOffset? DateFrom, DateTimeOffset? DateTo);
