using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Carts.Commands;

public class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, CartDto>
{
    private readonly ICartService _cartService;

    public AddCartItemCommandHandler(ICartService cartService)
    {
        _cartService = cartService;
    }

    public Task<CartDto> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
        => _cartService.AddItemAsync(request.Owner, request.ProductId, request.Quantity, cancellationToken);
}
