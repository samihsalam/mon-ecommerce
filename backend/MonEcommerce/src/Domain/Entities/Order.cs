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
    // Needed so the confirmation page (Story 4.6) can look up the order by payment intent id —
    // the browser only ever knows the client secret/payment intent id, never the server-side
    // Order.Id, since order creation happens asynchronously via a Stripe webhook.
    public string? StripePaymentIntentId { get; set; }
    public IList<OrderItem> Items { get; private set; } = new List<OrderItem>();
}
