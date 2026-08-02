using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Common;

// Extracted from AccountService (Story 2.5) — needed in a second place (Story 7.1's admin order
// list) for the exact same French display label, so it's now a shared single source of truth,
// same refactor precedent as OrderNumberFormatter/ReturnStatusLabelFormatter (Stories 5.3/5.4).
public static class OrderStatusLabelFormatter
{
    // Story 2.5 originally collapsed Pending+Processing into one shared "En préparation" label.
    // Story 7.2's own AC #5 names "En attente" and "En préparation" as two distinct stages in its
    // order-status transition graph — the PRD always intended these to be different, customer-
    // visible states. Corrected here at the source rather than left inconsistent with the
    // transition rules Story 7.2 builds against the same 5-value enum. See Story 7.2's Dev Notes.
    public static string Format(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "En attente",
        OrderStatus.Processing => "En préparation",
        OrderStatus.Shipped => "Expédiée",
        OrderStatus.Delivered => "Livrée",
        OrderStatus.Cancelled => "Annulée",
        _ => status.ToString(),
    };
}
