using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class ProductDailyViewCountConfiguration : IEntityTypeConfiguration<ProductDailyViewCount>
{
    public void Configure(EntityTypeBuilder<ProductDailyViewCount> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id);
        builder.HasOne(v => v.Product)
            .WithMany()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        // One row per product per day — also the read-then-write increment's duplicate guard
        // under a rare concurrent-request race (Story 7.5's Dev Notes).
        builder.HasIndex(v => new { v.ProductId, v.Date }).IsUnique().HasDatabaseName("ix_product_daily_view_counts_product_id_date");
    }
}
