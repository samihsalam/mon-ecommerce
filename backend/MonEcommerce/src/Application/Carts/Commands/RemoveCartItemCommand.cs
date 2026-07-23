using MonEcommerce.Application.Carts.Models;

namespace MonEcommerce.Application.Carts.Commands;

public record RemoveCartItemCommand(CartOwner Owner, Guid ItemId) : IRequest<CartDto>;
