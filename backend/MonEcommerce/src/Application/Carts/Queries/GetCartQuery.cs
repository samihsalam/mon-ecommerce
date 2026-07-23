using MonEcommerce.Application.Carts.Models;

namespace MonEcommerce.Application.Carts.Queries;

// No [Authorize] — the cart works for anonymous visitors too. Owner is resolved by the Web layer
// (authenticated user id, or the X-Cart-Session-Id header) and passed in explicitly, since
// MediatR handlers don't otherwise touch HttpContext/headers directly in this codebase.
public record GetCartQuery(CartOwner Owner) : IRequest<CartDto>;
