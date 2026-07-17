namespace MonEcommerce.Domain.Events;

public record OrderPlacedEvent(Guid OrderId, string UserId, string CustomerEmail, int TotalInCents) : BaseEvent;
