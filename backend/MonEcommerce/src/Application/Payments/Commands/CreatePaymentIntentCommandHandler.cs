using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Payments.Models;
using MonEcommerce.Application.Shipping;
using MonEcommerce.Domain.Entities;

namespace MonEcommerce.Application.Payments.Commands;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, CreatePaymentIntentResponse>
{
    private readonly ICartService _cartService;
    private readonly IPaymentService _paymentService;
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public CreatePaymentIntentCommandHandler(ICartService cartService, IPaymentService paymentService, IApplicationDbContext context, IUser user)
    {
        _cartService = cartService;
        _paymentService = paymentService;
        _context = context;
        _user = user;
    }

    public async Task<CreatePaymentIntentResponse> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartService.GetCartAsync(CartOwner.ForUser(_user.Id!), cancellationToken);
        if (cart.Items.Count == 0)
        {
            throw new ConflictException("Le panier est vide.");
        }

        // Validator already confirmed ShippingOptionId resolves — TryGetById cannot fail here.
        ShippingOptionsCatalog.TryGetById(request.ShippingOptionId, out var shippingOption);

        // The server, not the client, is the source of truth for both the cart total
        // (Product.PriceInCents, resolved inside CartService) and the shipping cost
        // (ShippingOptionsCatalog) — see Dev Notes on why this isn't literally just "the cart
        // total" per the AC's shorthand wording.
        var amountInCents = cart.TotalInCents + shippingOption!.PriceInCents;

        // Story 4.6: persist the address now, at intent-creation time — the webhook that
        // eventually confirms this payment has no access to the client-side CheckoutStore, so
        // this is the only point where the address is captured server-side at all. Becomes a
        // real Order.ShippingAddressId reference once HandleStripeWebhookCommandHandler creates
        // the Order (Story 4.3's Dev Notes deferred exactly this to this story).
        var address = new Address
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id!,
            Street = request.Street,
            City = request.City,
            PostalCode = request.PostalCode,
            Country = request.Country,
        };
        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        // Carried on the Stripe PaymentIntent itself so the (asynchronous, unauthenticated)
        // webhook can reconstruct which user/address/shipping option this payment belongs to —
        // see Story 4.6's Dev Notes.
        var metadata = new Dictionary<string, string>
        {
            ["userId"] = _user.Id!,
            ["shippingAddressId"] = address.Id.ToString(),
            ["shippingOptionId"] = request.ShippingOptionId,
        };

        var result = await _paymentService.CreatePaymentIntentAsync(amountInCents, metadata: metadata, ct: cancellationToken);
        return new CreatePaymentIntentResponse(result.ClientSecret);
    }
}
