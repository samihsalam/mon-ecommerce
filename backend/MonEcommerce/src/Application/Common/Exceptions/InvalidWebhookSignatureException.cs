namespace MonEcommerce.Application.Common.Exceptions;

// Thrown when a webhook payload's signature doesn't verify (Infrastructure/ExternalServices/
// StripePaymentService.cs catches Stripe.net's own StripeException and rethrows this instead) —
// a plain Application-layer type so Web's ProblemDetailsExceptionHandler can map it to 400
// without needing a Stripe.net package reference of its own (only Infrastructure has one).
public class InvalidWebhookSignatureException : Exception
{
    public InvalidWebhookSignatureException(string message) : base(message)
    {
    }
}
