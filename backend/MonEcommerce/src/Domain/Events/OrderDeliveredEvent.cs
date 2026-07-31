namespace MonEcommerce.Domain.Events;

public record OrderDeliveredEvent(Guid OrderId, string CustomerEmail) : BaseEvent;
