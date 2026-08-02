namespace MonEcommerce.Domain.Entities;

// One row per (ProductId, Date), incremented in place — not one row per view. AC #1's "last 7
// days" ranking only ever needs SUM(ViewCount) WHERE Date >= ..., and per-day rows keep that
// query's row count bounded regardless of traffic volume. BaseEntity, not BaseAuditableEntity —
// this is a pure counter, not an admin-attributable action.
public class ProductDailyViewCount : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public DateOnly Date { get; set; }
    public int ViewCount { get; set; }
}
