using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Common;

// Extracted from AccountService (Story 5.1) — needed in a second place (Story 5.3's
// UpdateReturnStatusCommandHandler, for the ReturnStatusUpdatedEvent's customer-facing email) for
// the exact same French display label, so it's now a shared single source of truth.
public static class ReturnStatusLabelFormatter
{
    public static string Format(ReturnStatus status) => status switch
    {
        ReturnStatus.Pending => "En attente",
        ReturnStatus.Approved => "Validé",
        ReturnStatus.Rejected => "Refusé",
        ReturnStatus.Refunded => "Remboursé",
        _ => status.ToString(),
    };
}
