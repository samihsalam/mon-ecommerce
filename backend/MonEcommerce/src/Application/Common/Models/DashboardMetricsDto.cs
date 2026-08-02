namespace MonEcommerce.Application.Common.Models;

// Field names use this codebase's own "...InCents" suffix convention (TotalInCents,
// PriceInCents, etc.) rather than the AC's literal bare "revenueToday"/"revenueThisMonth" — same
// unit (cents), consistent naming, not a functional deviation. RevenueTrendPercentage is null
// when yesterday's revenue was zero (no meaningful percentage change to compute against a zero
// baseline).
public record DashboardMetricsDto(
    int RevenueTodayInCents,
    int OrdersToday,
    int AverageOrderValueInCents,
    int RevenueThisMonthInCents,
    int RevenueYesterdayInCents,
    double? RevenueTrendPercentage);
