using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Payments.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(
        long amountInCents,
        string currency = "eur",
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken ct = default);

    Task<string> CreateRefundAsync(string paymentIntentId, long? amountInCents = null, CancellationToken ct = default);

    // Returns a plain Application-layer record, not Stripe.net's own Event/PaymentIntent types —
    // the Application project has no reference to Stripe.net at all (by design, same as
    // PaymentIntentResult already abstracting CreatePaymentIntentAsync's Stripe response), so
    // this keeps that boundary intact for webhook parsing too. Throws (a Stripe.net exception,
    // propagated as-is) when the signature doesn't verify — callers must never process an
    // unverified payload.
    WebhookEvent ParseWebhookEvent(string payload, string signatureHeader);

    // Story 8.3, AC #6: best-effort — this codebase's checkout flow never creates a Stripe
    // Customer object (CreatePaymentIntentAsync above has no Customer param), so this is expected
    // to be a no-op for essentially every real customer today. Searches by email and deletes the
    // first match if one exists; does nothing if none does. Propagates Stripe.net exceptions like
    // CreateRefundAsync does — the caller decides how to handle a failed request.
    Task DeleteCustomerDataAsync(string email, CancellationToken ct = default);
}
