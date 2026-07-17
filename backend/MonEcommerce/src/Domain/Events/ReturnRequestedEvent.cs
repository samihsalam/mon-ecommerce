namespace MonEcommerce.Domain.Events;

public record ReturnRequestedEvent(Guid ReturnId, Guid OrderId, string CustomerEmail, string Reason) : BaseEvent;
