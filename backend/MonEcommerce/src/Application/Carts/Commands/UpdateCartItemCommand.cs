using MonEcommerce.Application.Carts.Models;

namespace MonEcommerce.Application.Carts.Commands;

// Quantity 0 removes the item — AC #3's contract, enforced in CartService, not rejected here.
public record UpdateCartItemCommand(CartOwner Owner, Guid ItemId, int Quantity) : IRequest<CartDto>;
