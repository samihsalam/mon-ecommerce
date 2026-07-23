using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Carts.Commands;

public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, CartDto>
{
    private readonly ICartService _cartService;

    public UpdateCartItemCommandHandler(ICartService cartService)
    {
        _cartService = cartService;
    }

    public Task<CartDto> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
        => _cartService.UpdateItemQuantityAsync(request.Owner, request.ItemId, request.Quantity, cancellationToken);
}
