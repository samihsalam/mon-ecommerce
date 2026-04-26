namespace MonEcommerce.Domain.Entities;

public class Stock : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public int AlertThreshold { get; set; } = 5;
    public Guid? VendorId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();  // SQL Server rowversion concurrency token
}
