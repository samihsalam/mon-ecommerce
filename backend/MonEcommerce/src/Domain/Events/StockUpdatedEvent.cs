namespace MonEcommerce.Domain.Events;

// AC #2: raised only when NewQuantity <= AlertThreshold — an alert event, not a general
// "stock changed" event fired on every update. No INotificationHandler subscribes to this yet
// (no admin dashboard exists to show it on, and AC #2 marks email as merely optional) — publishing
// a MediatR notification with zero handlers is a harmless no-op; this is ready for Epic 7's
// dashboard (or a future opt-in email) to subscribe to without further plumbing.
public record StockUpdatedEvent(Guid ProductId, string ProductName, int NewQuantity, int AlertThreshold) : BaseEvent;
