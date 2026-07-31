namespace MonEcommerce.Domain.Events;

// TrackingLink (Story 5.2, AC #1): the pre-built full URL to the order's detail/tracking page —
// built where the event is published (Frontend:BaseUrl + IConfiguration, same convention as
// AuthService's password-reset link), not by the email handler, which has no config access of
// its own.
public record OrderShippedEvent(Guid OrderId, string CustomerEmail, string TrackingNumber, string TrackingLink) : BaseEvent;
