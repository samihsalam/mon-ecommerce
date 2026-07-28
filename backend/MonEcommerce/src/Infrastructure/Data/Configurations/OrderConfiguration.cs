using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id);
        builder.Property(o => o.Status).HasConversion<int>().IsRequired();
        builder.Property(o => o.TrackingNumber).HasMaxLength(200);
        builder.Property(o => o.StripePaymentIntentId).HasMaxLength(255);
        builder.HasOne(o => o.ShippingAddress)
            .WithMany()
            .HasForeignKey(o => o.ShippingAddressId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_orders_user_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_orders_status");
        // Unique + filtered (like Story 4.1's Cart owner indexes) — a payment intent confirms at
        // most one order, and this is also what GetOrderByPaymentIntentQuery looks up by.
        builder.HasIndex(o => o.StripePaymentIntentId)
            .IsUnique()
            .HasFilter("[StripePaymentIntentId] IS NOT NULL")
            .HasDatabaseName("ix_orders_stripe_payment_intent_id");
    }
}
