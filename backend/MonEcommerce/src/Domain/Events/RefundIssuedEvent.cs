namespace MonEcommerce.Domain.Events;

public record RefundIssuedEvent(Guid RefundId, Guid OrderId, string CustomerEmail, int AmountInCents) : BaseEvent;
