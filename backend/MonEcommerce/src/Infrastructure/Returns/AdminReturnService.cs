using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Returns.Models;
using MonEcommerce.Infrastructure.Identity;

namespace MonEcommerce.Infrastructure.Returns;

// Same "Infrastructure service does the real EF Core query, Application handler just delegates"
// split as AdminOrderService (Story 7.1) — customer-name resolution needs ApplicationUser data,
// which IApplicationDbContext deliberately doesn't expose.
public class AdminReturnService : IAdminReturnService
{
    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminReturnService(IApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<List<AdminReturnSummaryDto>> GetReturnsAsync(AdminReturnFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Returns.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(r => r.Created >= filter.DateFrom);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(r => r.Created <= filter.DateTo);
        }

        // AC #1 asks for "a list," not a paginated one — no Skip/Take, same restraint as Story
        // 6.4's GetStockHistoryQuery.
        var returns = await query
            .OrderByDescending(r => r.Created)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);

        // Customer names resolved in one batched query, not per row (same convention as
        // AdminOrderService).
        var userIds = returns.Select(r => r.UserId).Distinct().ToList();
        var namesById = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        return returns
            .Select(r => new AdminReturnSummaryDto(
                r.Id,
                OrderNumberFormatter.Format(r.OrderId),
                namesById.GetValueOrDefault(r.UserId, "—"),
                ReturnReasonLabelFormatter.Format(r.Reason),
                r.Created,
                ReturnStatusLabelFormatter.Format(r.Status),
                r.PhotoUrls))
            .ToList();
    }
}
