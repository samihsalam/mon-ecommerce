namespace MonEcommerce.Domain.Events;

public record UserRegisteredEvent(string UserId, string Name, string Email) : BaseEvent;
