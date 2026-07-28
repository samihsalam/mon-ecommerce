namespace MonEcommerce.Application.Payments.Models;

// Application-layer projection of a verified Stripe webhook event — deliberately not Stripe.net's
// own Event/PaymentIntent types (Application has no reference to Stripe.net at all; see
// IPaymentService.ParseWebhookEvent).
public record WebhookEvent(string Type, string? PaymentIntentId, long? AmountInCents, IReadOnlyDictionary<string, string> Metadata);
