using MonEcommerce.Application.Carts.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface ICartService
{
    Task<CartDto> GetCartAsync(CartOwner owner, CancellationToken cancellationToken = default);

    Task<CartDto> AddItemAsync(CartOwner owner, Guid productId, int quantity, CancellationToken cancellationToken = default);

    // quantity 0 removes the item — same "update to zero means delete" contract as AC #3.
    // Throws NotFoundException if itemId doesn't exist in THIS owner's cart (never a separate
    // ownership check after an unscoped lookup — see CartService's Dev Notes).
    Task<CartDto> UpdateItemQuantityAsync(CartOwner owner, Guid itemId, int quantity, CancellationToken cancellationToken = default);

    Task<CartDto> RemoveItemAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken = default);

    // No-ops cleanly if no anonymous cart exists for sessionId (already expired, or the visitor
    // never added anything before logging in).
    Task MergeAnonymousCartAsync(string sessionId, string userId, CancellationToken cancellationToken = default);
}
