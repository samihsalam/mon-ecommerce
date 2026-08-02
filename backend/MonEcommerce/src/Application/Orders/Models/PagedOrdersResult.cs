namespace MonEcommerce.Application.Orders.Models;

// Mirrors Catalogue.Models.PagedProductsResult<T>'s shape (AC #5's exact field names) —
// deliberately not reused directly (misleadingly named for products) nor unified with
// Account.Models.PagedResult<T>'s different, pre-existing shape. See Story 7.1's Dev Notes.
public record PagedOrdersResult<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize, int TotalPages);
