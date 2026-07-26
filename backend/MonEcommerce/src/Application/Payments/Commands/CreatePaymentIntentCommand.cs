using MonEcommerce.Application.Common.Security;
using MonEcommerce.Application.Payments.Models;

namespace MonEcommerce.Application.Payments.Commands;

// No CartOwner/UserId parameter — the handler resolves the current user via IUser directly
// (same convention as Account's commands/queries), since payment is only reachable by an
// authenticated customer (checkout requires login since Story 4.3) — there's no anonymous-cart
// case to support here, unlike Carts.cs's endpoint-level CartOwner resolution.
[Authorize]
public record CreatePaymentIntentCommand(string ShippingOptionId) : IRequest<CreatePaymentIntentResponse>;
