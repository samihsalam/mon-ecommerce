namespace MonEcommerce.Domain.Events;

// NewStatus is the French display label ("Validé"/"Refusé"), not the raw enum name — the email
// handler has no reason to re-derive/duplicate that mapping (already centralized in
// AccountService.MapReturnStatusLabel); the caller (UpdateReturnStatusCommandHandler) passes the
// same label the customer would see on their order detail page.
// Reason (Story 7.3, AC #4): only ever set on rejection, included in the email when present.
public record ReturnStatusUpdatedEvent(Guid ReturnId, Guid OrderId, string CustomerEmail, string NewStatus, string? Reason = null) : BaseEvent;
