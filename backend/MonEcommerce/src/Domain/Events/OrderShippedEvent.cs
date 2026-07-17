namespace MonEcommerce.Domain.Events;

public record OrderShippedEvent(Guid OrderId, string CustomerEmail, string TrackingNumber) : BaseEvent;
