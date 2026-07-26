using MonEcommerce.Application.Shipping.Models;

namespace MonEcommerce.Application.Shipping.Queries;

public class GetShippingOptionsQueryHandler : IRequestHandler<GetShippingOptionsQuery, IReadOnlyList<ShippingOptionDto>>
{
    // Hardcoded, not a database table — a fixed, non-admin-manageable list (see Dev Notes on
    // Story 4.4). "standard" is always first and always present, satisfying AC #6 trivially.
    // IReadOnlyList (not List) — this same instance is returned to every request for the
    // lifetime of the process (review finding: a plain List<T> exposed the shared static field
    // to any accidental future in-place mutation, e.g. Sort()/Add(), which would have silently
    // corrupted the list for every subsequent caller until app restart).
    private static readonly IReadOnlyList<ShippingOptionDto> Options =
    [
        new("standard", "Livraison Standard", 490, "3–5 jours ouvrés"),
        new("express", "Livraison Express", 990, "1–2 jours ouvrés"),
    ];

    public Task<IReadOnlyList<ShippingOptionDto>> Handle(GetShippingOptionsQuery request, CancellationToken cancellationToken)
        => Task.FromResult(Options);
}
