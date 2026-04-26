namespace MonEcommerce.Domain.Entities;

public class Order : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public int TotalInCents { get; set; }
    public Guid ShippingAddressId { get; set; }
    public Address ShippingAddress { get; set; } = null!;
    public string? TrackingNumber { get; set; }
    public Guid? VendorId { get; set; }
    public IList<OrderItem> Items { get; private set; } = new List<OrderItem>();
}
