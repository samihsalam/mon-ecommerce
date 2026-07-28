namespace MonEcommerce.Domain.Events;

// Deliberately NOT RefundIssuedEvent — that event assumes an Order already exists (Epic 5's
// post-delivery-return refund flow). This event fires when a Stripe payment succeeded but stock
// ran out before an Order could ever be created (Story 4.6's anti-overselling path) — there is no
// OrderId to reference.
public record StockUnavailableEvent(string CustomerEmail, int AmountInCents) : BaseEvent;
