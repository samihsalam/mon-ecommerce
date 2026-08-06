using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Domain.Entities;

// AC #4 (Story 8.3): this row IS the audit log — Status/ProcessedByAdminUserId/ProcessedAt
// capture "logged with timestamp and processing admin", same convention as Return/PaymentAuditLog
// (no separate audit table). No OriginalEmail field — the request-confirmation email (AC #1) is
// sent synchronously at request time using the customer's still-real email; freezing a copy here
// would work against AC #2's "irreversible" anonymization for the interim pending period.
public class AccountDeletionRequest : BaseAuditableEntity
{
    public string UserId { get; set; } = string.Empty;
    public AccountDeletionStatus Status { get; set; } = AccountDeletionStatus.Pending;
    public string? ProcessedByAdminUserId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
