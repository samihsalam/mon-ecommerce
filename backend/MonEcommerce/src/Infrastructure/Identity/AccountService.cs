using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Infrastructure.Identity;

public class AccountService : IAccountService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IApplicationDbContext _context;

    public AccountService(UserManager<ApplicationUser> userManager, IApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public async Task<ProfileDto> GetProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new AppNotFoundException(nameof(ApplicationUser), userId);

        var addresses = await LoadAddressesAsync(userId, cancellationToken);

        return new ProfileDto(user.Name, user.Email!, addresses);
    }

    public async Task<Result<ProfileDto>> UpdateProfileAsync(string userId, string name, string email, string? currentPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new AppNotFoundException(nameof(ApplicationUser), userId);

        var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase);
        var trimmedName = name.Trim();

        if (emailChanged)
        {
            if (string.IsNullOrEmpty(currentPassword))
            {
                return Result<ProfileDto>.Failure(["Le mot de passe actuel est requis pour changer d'email."]);
            }

            if (!await _userManager.CheckPasswordAsync(user, currentPassword))
            {
                return Result<ProfileDto>.Failure(["Mot de passe actuel incorrect."]);
            }

            // Narrow TOCTOU: two different accounts racing to claim the same new email could
            // both pass this check before either writes. Same class of pre-existing gap Story
            // 2.1 already flagged (no unique index on NormalizedEmail, RequireUniqueEmail unset)
            // — not re-solved here, tracked at the root cause rather than patched per-caller.
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null && existing.Id != user.Id)
            {
                return Result<ProfileDto>.Failure(["Un compte existe déjà avec cet email."]);
            }

            // Set Name on the tracked entity before the first Set*Async call: each one persists
            // immediately (via UserManager's internal UpdateUserAsync), and EF Core saves every
            // dirty property on the tracked entity in that same call — so Name rides along with
            // the first successful write instead of needing its own separate UpdateAsync, which
            // previously left a window where Email/UserName could be persisted while the overall
            // operation still reported failure (if that separate call then failed).
            user.Name = trimmedName;

            var setEmailResult = await _userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                return Result<ProfileDto>.Failure(setEmailResult.Errors.Select(e => e.Description));
            }

            var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                return Result<ProfileDto>.Failure(setUserNameResult.Errors.Select(e => e.Description));
            }
        }
        else
        {
            user.Name = trimmedName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Result<ProfileDto>.Failure(result.Errors.Select(e => e.Description));
            }
        }

        var addresses = await LoadAddressesAsync(userId, cancellationToken);

        return Result<ProfileDto>.Success(new ProfileDto(user.Name, user.Email!, addresses));
    }

    private async Task<List<AddressDto>> LoadAddressesAsync(string userId, CancellationToken cancellationToken)
    {
        return await _context.Addresses
            .Where(a => a.UserId == userId)
            .Select(a => new AddressDto(a.Id, a.Street, a.City, a.PostalCode, a.Country))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(string userId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // ThenByDescending(Id) is a tiebreaker: without a secondary sort key, two orders sharing
        // the same Created timestamp have no guaranteed stable order across separate Skip/Take
        // calls, which could duplicate or skip an order across two pages.
        var query = _context.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.Created)
            .ThenByDescending(o => o.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        // FormatOrderNumber/MapStatusLabel aren't translatable to SQL — materialize the page of
        // entities first, then project to DTOs client-side (LINQ to Objects).
        var pageOfOrders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var orders = pageOfOrders
            .Select(o => new OrderSummaryDto(o.Id, FormatOrderNumber(o.Id), o.Created, o.TotalInCents, MapStatusLabel(o.Status)))
            .ToList();

        return new PagedResult<OrderSummaryDto>(orders, totalCount, page, pageSize);
    }

    public async Task<OrderDetailDto> GetOrderDetailAsync(string userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        // Ownership is part of the query itself, not a separate check after an unscoped load —
        // see Story 2.5's Dev Notes. Same 404 whether the order doesn't exist or belongs to
        // someone else, so a customer can't distinguish the two by status code alone.
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Order), orderId);

        var items = order.Items
            .Select(i => new OrderItemDto(i.ProductName, i.UnitPriceInCents, i.Quantity))
            .ToList();

        var shippingAddress = new AddressDto(
            order.ShippingAddress.Id,
            order.ShippingAddress.Street,
            order.ShippingAddress.City,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country);

        return new OrderDetailDto(
            order.Id,
            FormatOrderNumber(order.Id),
            order.Created,
            order.TotalInCents,
            MapStatusLabel(order.Status),
            order.TrackingNumber,
            shippingAddress,
            items);
    }

    private static string FormatOrderNumber(Guid orderId) => $"#{orderId.ToString("N")[..8].ToUpperInvariant()}";

    // AC #5 lists 4 French labels for the 5-value OrderStatus enum — Pending and Processing
    // both map to "En préparation" (see Story 2.5's Dev Notes for the reasoning).
    private static string MapStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Pending => "En préparation",
        OrderStatus.Processing => "En préparation",
        OrderStatus.Shipped => "Expédiée",
        OrderStatus.Delivered => "Livrée",
        OrderStatus.Cancelled => "Annulée",
        _ => status.ToString(),
    };
}
