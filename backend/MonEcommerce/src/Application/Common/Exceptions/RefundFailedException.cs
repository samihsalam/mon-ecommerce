namespace MonEcommerce.Application.Common.Exceptions;

// Thrown when the Stripe refund API call itself fails (Story 5.3, AC #3) — a genuine upstream
// failure, distinct from every other exception in this codebase, none of which mean "an external
// payment provider call failed". Mapped to 502 Bad Gateway.
public class RefundFailedException : Exception
{
    public RefundFailedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
