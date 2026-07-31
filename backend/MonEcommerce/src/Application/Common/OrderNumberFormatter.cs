namespace MonEcommerce.Application.Common;

// Extracted from AccountService (Story 5.1/4.6) — needed in a second, unrelated place
// (IssueReturnRefundCommandHandler, Story 5.3) for the exact same customer-facing "#XXXXXXXX"
// order reference, so it's now a shared single source of truth rather than a second copy of the
// same one-liner.
public static class OrderNumberFormatter
{
    public static string Format(Guid orderId) => $"#{orderId.ToString("N")[..8].ToUpperInvariant()}";
}
