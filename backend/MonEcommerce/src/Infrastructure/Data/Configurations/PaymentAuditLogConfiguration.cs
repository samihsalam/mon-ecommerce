using MonEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MonEcommerce.Infrastructure.Data.Configurations;

public class PaymentAuditLogConfiguration : IEntityTypeConfiguration<PaymentAuditLog>
{
    public void Configure(EntityTypeBuilder<PaymentAuditLog> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id);
        builder.Property(p => p.StripePaymentIntentId).IsRequired().HasMaxLength(255);
        builder.Property(p => p.UserId).IsRequired().HasMaxLength(450);
        builder.Property(p => p.AdminUserId).HasMaxLength(450);
        builder.Property(p => p.StripeRefundId).HasMaxLength(255);
        builder.Property(p => p.Outcome).HasConversion<int>().IsRequired();
        // Not unique on its own — a duplicate webhook delivery for the SAME payment intent is
        // exactly the case the idempotency guard (HandleStripeWebhookCommandHandler) checks
        // for by querying this index, not something the schema itself needs to reject; rejecting
        // it here would turn a normal "Stripe retried" delivery into an unhandled 500 instead of
        // the intended clean no-op.
        builder.HasIndex(p => p.StripePaymentIntentId).HasDatabaseName("ix_payment_audit_logs_stripe_payment_intent_id");
    }
}
