using MonEcommerce.Application.Carts.Models;

namespace MonEcommerce.Application.Carts.Commands;

public record AddCartItemCommand(CartOwner Owner, Guid ProductId, int Quantity) : IRequest<CartDto>;
