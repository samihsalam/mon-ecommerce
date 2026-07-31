namespace MonEcommerce.Domain.Entities;

public class Product : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PriceInCents { get; set; }
    public string? Material { get; set; }
    public string? Color { get; set; }
    public string? Dimensions { get; set; }
    public bool IsPublished { get; set; }
    // Story 6.1: dedicated soft-delete flag, deliberately independent of IsPublished — Story 6.5
    // owns IsPublished exclusively via its own PATCH /publish endpoint, so a deleted product must
    // stay hidden regardless of whatever that endpoint later does to IsPublished.
    public bool IsDeleted { get; set; }
    public Guid? VendorId { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Stock? Stock { get; set; }
    public IList<ProductImage> Images { get; private set; } = new List<ProductImage>();
    public IList<OrderItem> OrderItems { get; private set; } = new List<OrderItem>();
    public IList<CartItem> CartItems { get; private set; } = new List<CartItem>();
}
