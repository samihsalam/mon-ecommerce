using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Carts.Commands;

public class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, CartDto>
{
    private readonly ICartService _cartService;

    public RemoveCartItemCommandHandler(ICartService cartService)
    {
        _cartService = cartService;
    }

    public Task<CartDto> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
        => _cartService.RemoveItemAsync(request.Owner, request.ItemId, cancellationToken);
}
