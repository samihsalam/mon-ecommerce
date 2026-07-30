using System.Text.Json;
using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class ReturnConfiguration : IEntityTypeConfiguration<Return>
{
    public void Configure(EntityTypeBuilder<Return> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id);
        builder.Property(r => r.Reason).HasConversion<int>().IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.Description).IsRequired().HasMaxLength(2000);

        // Simple string list, no separate child table needed for a handful of photo URLs per
        // return — JSON-serialized into a single column, same pragmatic choice as any other
        // "small list of primitives" field would get in this codebase (no precedent to follow
        // either way, but a child table would be pure overhead here).
        builder.Property(r => r.PhotoUrls)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                v => v.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                v => v.ToList()));

        builder.Property(r => r.PhotoUrls).HasColumnType("nvarchar(max)");

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.UserId).HasDatabaseName("ix_returns_user_id");
        builder.HasIndex(r => r.OrderId).HasDatabaseName("ix_returns_order_id");
    }
}
