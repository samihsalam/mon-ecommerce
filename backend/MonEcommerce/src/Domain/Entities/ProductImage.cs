namespace MonEcommerce.Domain.Entities;

public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    // Story 6.2: the Cloudinary asset id, needed to actually delete the asset (FileUploadResult
    // already returns it; Story 5.1's return photos never persisted it since they're never
    // deleted via the Cloudinary API).
    public string PublicId { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
