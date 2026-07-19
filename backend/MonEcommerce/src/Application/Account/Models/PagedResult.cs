namespace MonEcommerce.Application.Account.Models;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
