namespace MonEcommerce.Domain.Entities;

public class Cart : BaseAuditableEntity
{
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
    public IList<CartItem> Items { get; private set; } = new List<CartItem>();
}
