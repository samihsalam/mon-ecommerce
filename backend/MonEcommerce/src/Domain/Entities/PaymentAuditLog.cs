namespace MonEcommerce.Domain.Entities;

// One row per webhook event actually processed (AC #8's "audit trail (non-deletable)") — also
// doubles as the idempotency guard for duplicate Stripe webhook deliveries (see
// HandleStripeWebhookCommandHandler). BaseAuditableEntity (not plain BaseEntity) so it gets
// Created for free from the existing AuditableEntityInterceptor — LastModified is simply never
// meaningfully touched after insert, since no code path here ever updates or deletes a row.
public class PaymentAuditLog : BaseAuditableEntity
{
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int AmountInCents { get; set; }
    public PaymentAuditOutcome Outcome { get; set; }
    public Guid? OrderId { get; set; }

    // Both null for Story 4.6's webhook-originated rows (no admin involved, no Stripe refund
    // object — CreateRefundAsync isn't called on that path either); both set for Story 5.3's
    // admin-issued return refunds (AC #5's "admin user, Stripe refund ID").
    public string? AdminUserId { get; set; }
    public string? StripeRefundId { get; set; }
}
