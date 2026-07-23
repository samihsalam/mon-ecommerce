using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Carts.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Infrastructure.Carts;

public class CartService : ICartService
{
    // AC #4's read-time approximation of the AC's literal "Redis TTL 24h" — an anonymous
    // (SessionId-owned) cart untouched for this long is treated as expired and replaced on next
    // access. Authenticated (UserId-owned) carts never expire — see IsExpired.
    private static readonly TimeSpan AnonymousCartExpiry = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public CartService(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<CartDto> GetCartAsync(CartOwner owner, CancellationToken cancellationToken = default)
    {
        var cart = await FindCartWithItemsAsync(owner, cancellationToken);
        return cart == null || IsExpired(cart) ? new CartDto([], 0) : MapToDto(cart);
    }

    public async Task<CartDto> AddItemAsync(CartOwner owner, Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        // IsPublished filter matches this codebase's established convention for "customer-visible"
        // (every product-facing query in ProductCatalogueService filters on it) — without it, a
        // caller who knows/guesses a draft product's id could add it to their cart and see its
        // name/price/image via GET /cart, leaking unreleased catalogue data through a path that
        // isn't supposed to expose it.
        var productExists = await _context.Products.AnyAsync(p => p.Id == productId && p.IsPublished, cancellationToken);
        if (!productExists)
        {
            throw new AppNotFoundException(nameof(Product), productId);
        }

        var cart = await FindOrCreateActiveCartAsync(owner, cancellationToken);

        var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
        if (existingItem != null)
        {
            existingItem.Quantity += quantity;
        }
        else
        {
            // Added directly to the CartItems DbSet, NOT via cart.Items.Add(...) — adding a new
            // child through an already-persisted parent's collection navigation (the parent was
            // saved in FindOrCreateActiveCartAsync's own SaveChangesAsync just above, a SEPARATE
            // call from this one) trips a genuine EF Core InMemory provider bug: it throws
            // DbUpdateConcurrencyException ("entity does not exist in the store") on the next
            // save, apparently misinterpreting the fixed-up parent reference as an update to a
            // stale/missing row. Confirmed via an isolated minimal repro against a real
            // ApplicationDbContext — SQL Server is not expected to have this limitation, but
            // adding to the DbSet directly is the more conventional pattern anyway and sidesteps
            // it entirely on both providers.
            _context.CartItems.Add(new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = cart.Id,
                ProductId = productId,
                Quantity = quantity,
            });
        }

        cart.LastModified = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCartAsync(owner, cancellationToken);
    }

    public async Task<CartDto> UpdateItemQuantityAsync(CartOwner owner, Guid itemId, int quantity, CancellationToken cancellationToken = default)
    {
        var item = await GetOwnedItemAsync(owner, itemId, cancellationToken);

        // quantity 0 removes the item — AC #3's contract.
        if (quantity == 0)
        {
            _context.CartItems.Remove(item);
        }
        else
        {
            item.Quantity = quantity;
        }

        item.Cart.LastModified = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCartAsync(owner, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken = default)
    {
        var item = await GetOwnedItemAsync(owner, itemId, cancellationToken);

        _context.CartItems.Remove(item);
        item.Cart.LastModified = _timeProvider.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return await GetCartAsync(owner, cancellationToken);
    }

    public async Task MergeAnonymousCartAsync(string sessionId, string userId, CancellationToken cancellationToken = default)
    {
        var anonymousCart = await FindCartWithItemsAsync(CartOwner.ForSession(sessionId), cancellationToken);
        if (anonymousCart == null || IsExpired(anonymousCart))
        {
            return;
        }

        var userCart = await FindCartWithItemsAsync(CartOwner.ForUser(userId), cancellationToken);
        if (userCart == null)
        {
            userCart = new Domain.Entities.Cart { Id = Guid.NewGuid(), UserId = userId };
            _context.Carts.Add(userCart);
        }

        // Merges by creating fresh CartItem rows in userCart and deleting the whole anonymous
        // cart (cascade-deletes its old items) afterward, rather than re-parenting the existing
        // rows — simpler, and item ids aren't meant to be stable across a merge event.
        foreach (var anonymousItem in anonymousCart.Items)
        {
            var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == anonymousItem.ProductId);
            if (existingItem != null)
            {
                existingItem.Quantity += anonymousItem.Quantity;
            }
            else
            {
                // Same DbSet-direct-add reasoning as AddItemAsync — userCart may already be a
                // persisted (Unchanged) entity from FindCartWithItemsAsync above.
                _context.CartItems.Add(new CartItem
                {
                    Id = Guid.NewGuid(),
                    CartId = userCart.Id,
                    ProductId = anonymousItem.ProductId,
                    Quantity = anonymousItem.Quantity,
                });
            }
        }

        userCart.LastModified = _timeProvider.GetUtcNow();
        _context.Carts.Remove(anonymousCart);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Domain.Entities.Cart> FindOrCreateActiveCartAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var cart = await FindCartWithItemsAsync(owner, cancellationToken);

        if (cart != null && IsExpired(cart))
        {
            _context.Carts.Remove(cart);
            await _context.SaveChangesAsync(cancellationToken);
            cart = null;
        }

        if (cart == null)
        {
            cart = new Domain.Entities.Cart
            {
                Id = Guid.NewGuid(),
                UserId = owner.UserId,
                SessionId = owner.SessionId,
            };
            _context.Carts.Add(cart);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // The unique filtered index on UserId/SessionId (see CartConfiguration) is what
                // actually prevents the split-cart race — this catch just keeps the LOSING side of
                // that race from surfacing as an unhandled 500. A concurrent request for the same
                // owner won between our find and our insert; re-fetch and use its cart instead of
                // failing this one.
                cart = await FindCartWithItemsAsync(owner, cancellationToken)
                    ?? throw new AppNotFoundException(nameof(Domain.Entities.Cart), owner.UserId ?? owner.SessionId ?? string.Empty);
            }
        }

        return cart;
    }

    private async Task<Domain.Entities.Cart?> FindCartWithItemsAsync(CartOwner owner, CancellationToken cancellationToken)
    {
        var query = _context.Carts.Include(c => c.Items).ThenInclude(i => i.Product).ThenInclude(p => p.Images);

        return owner.IsAuthenticated
            ? await query.FirstOrDefaultAsync(c => c.UserId == owner.UserId, cancellationToken)
            : await query.FirstOrDefaultAsync(c => c.SessionId == owner.SessionId, cancellationToken);
    }

    // Scoped by owner in the SAME query, not looked up by itemId alone and then ownership-checked
    // afterward — the IDOR-prevention pattern established since Story 2.4/2.5.
    private async Task<CartItem> GetOwnedItemAsync(CartOwner owner, Guid itemId, CancellationToken cancellationToken)
    {
        var query = _context.CartItems.Include(i => i.Cart).Where(i => i.Id == itemId);

        query = owner.IsAuthenticated
            ? query.Where(i => i.Cart.UserId == owner.UserId)
            : query.Where(i => i.Cart.SessionId == owner.SessionId);

        var item = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppNotFoundException(nameof(CartItem), itemId);

        // GetCartAsync/FindOrCreateActiveCartAsync both treat an expired anonymous cart as gone —
        // this lookup must match that, or an itemId minted before expiry could still be mutated
        // via PATCH/DELETE after the 24h window, inconsistent with what GET already shows.
        if (IsExpired(item.Cart))
        {
            throw new AppNotFoundException(nameof(CartItem), itemId);
        }

        return item;
    }

    private bool IsExpired(Domain.Entities.Cart cart)
        => cart.UserId == null && _timeProvider.GetUtcNow() - cart.LastModified > AnonymousCartExpiry;

    private static CartDto MapToDto(Domain.Entities.Cart cart)
    {
        // LineTotalInCents itself is computed via long then narrowed — AddCartItemCommandValidator
        // caps quantity at 1,000, which keeps any single line comfortably within int range for any
        // realistic product price, but the multiplication is done in long first as cheap insurance.
        var items = cart.Items
            .Select(i => new CartItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Product.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.Url,
                i.Product.PriceInCents,
                i.Quantity,
                (int)Math.Min((long)i.Product.PriceInCents * i.Quantity, int.MaxValue)))
            .ToList();

        // Summed as long, then clamped rather than left as a plain int Sum() — Enumerable.Sum(int)
        // uses checked arithmetic internally and THROWS OverflowException once the aggregate of
        // many line totals exceeds int.MaxValue (a cart with enough distinct, near-cap line items),
        // and ProblemDetailsExceptionHandler has no case for that exception type — it would
        // otherwise surface as a raw, unhandled 500 on GET /cart instead of a usable response.
        var totalInCents = items.Sum(i => (long)i.LineTotalInCents);
        return new CartDto(items, (int)Math.Min(totalInCents, int.MaxValue));
    }
}
