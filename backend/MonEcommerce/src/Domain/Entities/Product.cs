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
    public Guid? VendorId { get; set; }
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Stock? Stock { get; set; }
    public IList<ProductImage> Images { get; private set; } = new List<ProductImage>();
    public IList<OrderItem> OrderItems { get; private set; } = new List<OrderItem>();
    public IList<CartItem> CartItems { get; private set; } = new List<CartItem>();
}
