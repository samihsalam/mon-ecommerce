using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id);
        builder.Property(s => s.AlertThreshold).HasDefaultValue(5);
        builder.HasOne(s => s.Product)
            .WithOne(p => p.Stock)
            .HasForeignKey<Stock>(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => s.ProductId).IsUnique();
        builder.Property(s => s.RowVersion)
            .IsRowVersion()
            .IsRequired();
    }
}
