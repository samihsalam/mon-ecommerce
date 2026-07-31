namespace MonEcommerce.Domain.Events;

// OrderNumber (Story 5.3, AC #4): pre-formatted "#XXXXXXXX" — the email must show a customer-
// facing order reference, not the raw OrderId Guid.
public record RefundIssuedEvent(Guid RefundId, Guid OrderId, string CustomerEmail, int AmountInCents, string OrderNumber) : BaseEvent;
