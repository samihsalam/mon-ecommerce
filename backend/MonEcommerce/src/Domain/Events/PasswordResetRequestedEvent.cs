namespace MonEcommerce.Domain.Events;

public record PasswordResetRequestedEvent(string UserId, string Name, string Email, string ResetLink) : BaseEvent;
