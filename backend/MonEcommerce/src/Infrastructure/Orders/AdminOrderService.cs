using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Orders.Models;
using MonEcommerce.Infrastructure.Identity;

namespace MonEcommerce.Infrastructure.Orders;

// Same "Infrastructure service does the real EF Core query, Application handler just delegates"
// split as AccountService/ProductCatalogueService — needed here specifically because filtering by
// customer name requires ApplicationUser data, which IApplicationDbContext deliberately doesn't
// expose (Identity stays out of the Application layer).
public class AdminOrderService : IAdminOrderService
{
    // Same overflow-guard convention as ProductCatalogueService: bounds (pageNumber - 1) * pageSize
    // so a crafted pageNumber can't wrap 32-bit int arithmetic into a negative Skip().
    private const int MaxPageNumber = 1_000_000;

    private readonly IApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminOrderService(IApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<PagedOrdersResult<AdminOrderSummaryDto>> GetOrdersAsync(AdminOrderFilter filter, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Clamp(filter.PageNumber, 1, MaxPageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status);
        }

        if (filter.DateFrom.HasValue)
        {
            query = query.Where(o => o.Created >= filter.DateFrom);
        }

        if (filter.DateTo.HasValue)
        {
            query = query.Where(o => o.Created <= filter.DateTo);
        }

        // AC #2's "search=Salma" — interpreted as a customer-name search (Order has no other
        // free-text field an admin would plausibly type). Two bounded queries (find matching user
        // ids, then filter orders by that id list), not one query per order — avoids N+1 without
        // relying on Order having an EF navigation to ApplicationUser (it doesn't; Identity's
        // tables are deliberately not modeled as a foreign relationship from Order).
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim().ToLower();
            var matchingUserIds = await _userManager.Users
                .Where(u => u.Name.ToLower().Contains(term))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(o => matchingUserIds.Contains(o.UserId));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // AC #3: date descending by default. Id as a tiebreaker — same convention as
        // AccountService.GetOrdersAsync — so two orders sharing a Created timestamp don't
        // duplicate/skip across separate Skip/Take pages.
        var pageOfOrders = await query
            .OrderByDescending(o => o.Created)
            .ThenByDescending(o => o.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Customer names for the current page resolved in one batched query, not per row.
        var userIds = pageOfOrders.Select(o => o.UserId).Distinct().ToList();
        var namesById = await _userManager.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var items = pageOfOrders
            .Select(o => new AdminOrderSummaryDto(
                o.Id,
                OrderNumberFormatter.Format(o.Id),
                namesById.GetValueOrDefault(o.UserId, "—"),
                o.Created,
                o.TotalInCents,
                OrderStatusLabelFormatter.Format(o.Status)))
            .ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedOrdersResult<AdminOrderSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);
    }
}
