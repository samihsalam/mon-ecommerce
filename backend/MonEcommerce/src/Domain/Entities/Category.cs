namespace MonEcommerce.Domain.Entities;

public class Category : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Category? Parent { get; set; }
    public IList<Category> Children { get; private set; } = new List<Category>();
    public IList<Product> Products { get; private set; } = new List<Product>();
}
