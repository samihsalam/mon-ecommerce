using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Carts.Queries;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, CartDto>
{
    private readonly ICartService _cartService;

    public GetCartQueryHandler(ICartService cartService)
    {
        _cartService = cartService;
    }

    public Task<CartDto> Handle(GetCartQuery request, CancellationToken cancellationToken)
        => _cartService.GetCartAsync(request.Owner, cancellationToken);
}
