using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Payments.Models;
using MonEcommerce.Application.Shipping;

namespace MonEcommerce.Application.Payments.Commands;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, CreatePaymentIntentResponse>
{
    private readonly ICartService _cartService;
    private readonly IPaymentService _paymentService;
    private readonly IUser _user;

    public CreatePaymentIntentCommandHandler(ICartService cartService, IPaymentService paymentService, IUser user)
    {
        _cartService = cartService;
        _paymentService = paymentService;
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

        var result = await _paymentService.CreatePaymentIntentAsync(amountInCents, ct: cancellationToken);
        return new CreatePaymentIntentResponse(result.ClientSecret);
    }
}
