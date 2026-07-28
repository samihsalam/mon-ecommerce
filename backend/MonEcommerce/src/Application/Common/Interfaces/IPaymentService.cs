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
}
