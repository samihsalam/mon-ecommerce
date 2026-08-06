namespace MonEcommerce.Domain.Events;

public record AccountDeletionRequestedEvent(Guid RequestId, string CustomerEmail) : BaseEvent;
