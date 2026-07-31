namespace MonEcommerce.Domain.Entities;

// One row per logical send (not per retry attempt — AttemptCount records how many tries it took),
// matching PaymentAuditLog's "one row per outcome" convention (Story 4.6). BaseAuditableEntity
// for the free Created timestamp (AC #2's "timestamp").
public class EmailDispatchLog : BaseAuditableEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? SendGridMessageId { get; set; }
    public bool Success { get; set; }
    public int AttemptCount { get; set; }
    public string? ErrorMessage { get; set; }
}
