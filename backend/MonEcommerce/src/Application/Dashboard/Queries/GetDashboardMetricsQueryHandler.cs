using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Models;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Dashboard.Queries;

// No separate Infrastructure service (unlike Story 7.1/7.3's AdminOrderService/AdminReturnService)
// — these are pure Order aggregates, no ApplicationUser/UserManager data involved, so there's
// nothing IApplicationDbContext can't already provide directly. See Story 7.4's Dev Notes.
public class GetDashboardMetricsQueryHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetDashboardMetricsQueryHandler(IApplicationDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var yesterdayStart = todayStart.AddDays(-1);
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        // A cancelled order generated no real revenue or business-health signal — excluded from
        // every metric below (Story 7.4's Dev Notes).
        var revenueGeneratingOrders = _context.Orders.AsNoTracking().Where(o => o.Status != OrderStatus.Cancelled);

        // SQL-level aggregation throughout (SumAsync/CountAsync) — never materializing the Orders
        // table into memory (AC #6's ≤1s design property).
        var todayOrders = revenueGeneratingOrders.Where(o => o.Created >= todayStart);
        var revenueTodayInCents = await todayOrders.SumAsync(o => (int?)o.TotalInCents, cancellationToken) ?? 0;
        var ordersToday = await todayOrders.CountAsync(cancellationToken);

        var revenueYesterdayInCents = await revenueGeneratingOrders
            .Where(o => o.Created >= yesterdayStart && o.Created < todayStart)
            .SumAsync(o => (int?)o.TotalInCents, cancellationToken) ?? 0;

        var revenueThisMonthInCents = await revenueGeneratingOrders
            .Where(o => o.Created >= monthStart)
            .SumAsync(o => (int?)o.TotalInCents, cancellationToken) ?? 0;

        var averageOrderValueInCents = ordersToday > 0 ? revenueTodayInCents / ordersToday : 0;

        // Null when yesterday had zero revenue — no meaningful percentage to compute against a
        // zero baseline (would otherwise be an undefined/infinite "increase").
        double? revenueTrendPercentage = revenueYesterdayInCents > 0
            ? Math.Round((revenueTodayInCents - revenueYesterdayInCents) / (double)revenueYesterdayInCents * 100, 1)
            : null;

        return new DashboardMetricsDto(
            revenueTodayInCents,
            ordersToday,
            averageOrderValueInCents,
            revenueThisMonthInCents,
            revenueYesterdayInCents,
            revenueTrendPercentage);
    }
}
