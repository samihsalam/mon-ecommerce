using MonEcommerce.Application.Shipping.Models;

namespace MonEcommerce.Application.Shipping.Queries;

// No parameters, no [Authorize] — the same fixed list of options applies to every customer,
// nothing user-specific (see Web/Endpoints/ShippingOptions.cs).
public record GetShippingOptionsQuery : IRequest<IReadOnlyList<ShippingOptionDto>>;
