using System.Diagnostics.CodeAnalysis;
using MonEcommerce.Application.Shipping.Models;

namespace MonEcommerce.Application.Shipping;

// Hardcoded, not a database table — a fixed, non-admin-manageable list (see Story 4.4's Dev
// Notes). The single source of truth for both GetShippingOptionsQueryHandler (listing) and
// CreatePaymentIntentCommandHandler (Story 4.5 — resolving the authoritative price server-side,
// never trusting a client-sent amount), so the two can never silently drift out of sync.
public static class ShippingOptionsCatalog
{
    public static readonly IReadOnlyList<ShippingOptionDto> Options =
    [
        new("standard", "Livraison Standard", 490, "3–5 jours ouvrés"),
        new("express", "Livraison Express", 990, "1–2 jours ouvrés"),
    ];

    public static bool TryGetById(string id, [NotNullWhen(true)] out ShippingOptionDto? option)
    {
        option = Options.FirstOrDefault(o => o.Id == id);
        return option != null;
    }
}
