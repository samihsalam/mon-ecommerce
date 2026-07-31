namespace MonEcommerce.Domain.Events;

// NewStatus is the French display label ("Validé"/"Refusé"), not the raw enum name — the email
// handler has no reason to re-derive/duplicate that mapping (already centralized in
// AccountService.MapReturnStatusLabel); the caller (UpdateReturnStatusCommandHandler) passes the
// same label the customer would see on their order detail page.
public record ReturnStatusUpdatedEvent(Guid ReturnId, Guid OrderId, string CustomerEmail, string NewStatus) : BaseEvent;
