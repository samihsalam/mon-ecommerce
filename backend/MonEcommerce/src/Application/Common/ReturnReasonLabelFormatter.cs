using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Common;

// Extracted from AccountService (Story 5.1) — needed in a second place (Story 7.3's admin returns
// list) for the exact same French display label, so it's now a shared single source of truth,
// same refactor precedent as OrderStatusLabelFormatter (Story 7.1).
public static class ReturnReasonLabelFormatter
{
    public static string Format(ReturnReason reason) => reason switch
    {
        ReturnReason.WrongSize => "Mauvaise taille",
        ReturnReason.DefectiveProduct => "Produit défectueux",
        ReturnReason.NotAsDescribed => "Non conforme à la description",
        ReturnReason.ChangedMind => "Changement d'avis",
        ReturnReason.Other => "Autre",
        _ => reason.ToString(),
    };
}
