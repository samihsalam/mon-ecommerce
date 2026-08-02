using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Dashboard.Queries;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Infrastructure.Data;
using NUnit.Framework;

namespace MonEcommerce.Application.UnitTests.Dashboard.Queries;

public class GetDashboardMetricsQueryHandlerTests
{
    private class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public void SetNow(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private ApplicationDbContext _context = null!;
    private ManualTimeProvider _timeProvider = null!;
    private GetDashboardMetricsQueryHandler _handler = null!;

    // A fixed instant well inside the day/month so today/yesterday/this-month boundaries are
    // unambiguous regardless of when the test suite actually runs.
    private static readonly DateTimeOffset Now = new(2026, 4, 15, 14, 30, 0, TimeSpan.Zero);

    [SetUp]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        _timeProvider = new ManualTimeProvider();
        _timeProvider.SetNow(Now);

        _handler = new GetDashboardMetricsQueryHandler(_context, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    private Order SeedOrder(DateTimeOffset created, int totalInCents, OrderStatus status = OrderStatus.Pending)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            Status = status,
            TotalInCents = totalInCents,
            ShippingAddressId = Guid.NewGuid(),
            Created = created,
        };
        _context.Orders.Add(order);
        return order;
    }

    [Test]
    public async Task Handle_ShouldSumTodaysRevenueAndCountOrdersPlacedToday()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero), 10000);
        SeedOrder(new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero), 5000);
        // Yesterday — must not count toward "today."
        SeedOrder(new DateTimeOffset(2026, 4, 14, 23, 59, 0, TimeSpan.Zero), 7000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueTodayInCents, Is.EqualTo(15000));
        Assert.That(result.OrdersToday, Is.EqualTo(2));
        Assert.That(result.AverageOrderValueInCents, Is.EqualTo(7500));
    }

    [Test]
    public async Task Handle_ShouldReturnZeroesWhenThereAreNoOrdersToday()
    {
        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueTodayInCents, Is.EqualTo(0));
        Assert.That(result.OrdersToday, Is.EqualTo(0));
        // Division-by-zero guarded, not NaN/exception.
        Assert.That(result.AverageOrderValueInCents, Is.EqualTo(0));
    }

    [Test]
    public async Task Handle_ShouldExcludeCancelledOrdersFromEveryMetric()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero), 10000);
        SeedOrder(new DateTimeOffset(2026, 4, 15, 9, 0, 0, TimeSpan.Zero), 99999, OrderStatus.Cancelled);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueTodayInCents, Is.EqualTo(10000));
        Assert.That(result.OrdersToday, Is.EqualTo(1));
    }

    [Test]
    public async Task Handle_ShouldSumRevenueThisMonthAcrossTheWholeMonth()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 1, 0, 0, 1, TimeSpan.Zero), 1000);
        SeedOrder(new DateTimeOffset(2026, 4, 15, 8, 0, 0, TimeSpan.Zero), 2000);
        // Last month — must not count.
        SeedOrder(new DateTimeOffset(2026, 3, 31, 23, 59, 0, TimeSpan.Zero), 5000);
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueThisMonthInCents, Is.EqualTo(3000));
    }

    [Test]
    public async Task Handle_ShouldComputeThePositiveTrendPercentageAgainstYesterday()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero), 10000); // yesterday
        SeedOrder(new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero), 15000); // today
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueYesterdayInCents, Is.EqualTo(10000));
        Assert.That(result.RevenueTrendPercentage, Is.EqualTo(50.0));
    }

    [Test]
    public async Task Handle_ShouldComputeANegativeTrendPercentageWhenRevenueDropped()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 14, 10, 0, 0, TimeSpan.Zero), 20000); // yesterday
        SeedOrder(new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero), 10000); // today
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueTrendPercentage, Is.EqualTo(-50.0));
    }

    [Test]
    public async Task Handle_ShouldReturnNullTrendPercentageWhenYesterdayHadNoRevenue()
    {
        SeedOrder(new DateTimeOffset(2026, 4, 15, 10, 0, 0, TimeSpan.Zero), 10000); // today only
        await _context.SaveChangesAsync(CancellationToken.None);

        var result = await _handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.That(result.RevenueYesterdayInCents, Is.EqualTo(0));
        Assert.That(result.RevenueTrendPercentage, Is.Null);
    }
}
