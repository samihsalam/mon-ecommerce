using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Common;

// Extracted from AccountService (Story 2.5) — needed in a second place (Story 7.1's admin order
// list) for the exact same French display label, so it's now a shared single source of truth,
// same refactor precedent as OrderNumberFormatter/ReturnStatusLabelFormatter (Stories 5.3/5.4).
public static class OrderStatusLabelFormatter
{
    // Pending and Processing both map to "En préparation" (see Story 2.5's Dev Notes) — the AC's
    // 4 French labels cover the 5-value OrderStatus enum.
    public static string Format(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "En préparation",
        OrderStatus.Processing => "En préparation",
        OrderStatus.Shipped => "Expédiée",
        OrderStatus.Delivered => "Livrée",
        OrderStatus.Cancelled => "Annulée",
        _ => status.ToString(),
    };
}
