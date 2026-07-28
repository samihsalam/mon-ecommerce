namespace MonEcommerce.Application.Payments.Webhooks;

// No [Authorize] — Stripe calls this endpoint directly with no user session; the payload's
// signature (verified inside the handler via IPaymentService.ParseWebhookEvent) is the only
// security boundary. Payload/SignatureHeader are the raw request body and Stripe-Signature
// header exactly as received — the signature check needs the untouched bytes, not a re-serialized
// re-parse of them.
public record HandleStripeWebhookCommand(string Payload, string SignatureHeader) : IRequest;
