using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(300);
        builder.Property(p => p.Description).IsRequired();
        builder.Property(p => p.Material).HasMaxLength(200);
        builder.Property(p => p.Color).HasMaxLength(100);
        builder.Property(p => p.Dimensions).HasMaxLength(200);
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.CategoryId).HasDatabaseName("ix_products_category_id");
    }
}
