namespace MonEcommerce.Domain.Entities;

// One row per PATCH /admin/products/{id}/stock call — BaseAuditableEntity for the free
// CreatedBy/Created (AC #1's "admin, timestamp"), same convention as PaymentAuditLog/
// EmailDispatchLog.
public class StockMovement : BaseAuditableEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}
