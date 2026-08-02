using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Domain.Entities;

// One row per PATCH /admin/orders/{id}/status call — BaseAuditableEntity for the free
// CreatedBy/Created (AC #4's "admin user ID, timestamp"), same convention as StockMovement
// (Story 6.4)/PaymentAuditLog/EmailDispatchLog.
public class OrderStatusHistory : BaseAuditableEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus NewStatus { get; set; }
}
