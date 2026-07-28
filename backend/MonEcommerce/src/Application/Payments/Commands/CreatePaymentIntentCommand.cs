using MonEcommerce.Application.Common.Security;
using MonEcommerce.Application.Payments.Models;

namespace MonEcommerce.Application.Payments.Commands;

// No CartOwner/UserId parameter — the handler resolves the current user via IUser directly
// (same convention as Account's commands/queries), since payment is only reachable by an
// authenticated customer (checkout requires login since Story 4.3) — there's no anonymous-cart
// case to support here, unlike Carts.cs's endpoint-level CartOwner resolution.
//
// Street/City/PostalCode/Country (Story 4.6): the webhook that eventually confirms this payment
// has no access to the client-side CheckoutStore, so the address has to be captured and
// persisted here, at intent-creation time, and threaded through to the webhook via Stripe
// PaymentIntent metadata — see CreatePaymentIntentCommandHandler and Story 4.6's Dev Notes.
[Authorize]
public record CreatePaymentIntentCommand(
    string ShippingOptionId,
    string Street,
    string City,
    string PostalCode,
    string Country) : IRequest<CreatePaymentIntentResponse>;
