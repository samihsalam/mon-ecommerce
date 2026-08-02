using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Infrastructure.Catalogue;

public class AnalyticsService : IAnalyticsService
{
    private const string TopProductsCacheKey = "analytics:top-products";
    private const int TopProductsLimit = 10;
    private static readonly TimeSpan TopProductsTtl = TimeSpan.FromHours(1);

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _timeProvider;

    public AnalyticsService(IApplicationDbContext context, ICacheService cache, IConfiguration configuration, TimeProvider timeProvider)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
        _timeProvider = timeProvider;
    }

    public async Task<TopProductsDto> GetTopProductsAsync(CancellationToken cancellationToken = default)
    {
        // AC #3: cached in Redis, 1-hour TTL.
        var cached = await _cache.GetAsync<TopProductsDto>(TopProductsCacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var now = _timeProvider.GetUtcNow();
        var sevenDaysAgo = now.AddDays(-7);
        // ProductDailyViewCount is aggregated at day granularity, so the cutoff here is a calendar
        // date, not an exact timestamp — a day's worth more precision than OrderItem's cutoff
        // below, which compares against Order.Created directly. Both are "last 7 days" at a
        // reasonable, if not perfectly identical, granularity.
        var sevenDaysAgoDate = DateOnly.FromDateTime(sevenDaysAgo.UtcDateTime);

        var mostViewed = await _context.ProductDailyViewCounts
            .AsNoTracking()
            .Where(v => v.Date >= sevenDaysAgoDate)
            .GroupBy(v => v.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Sum(v => v.ViewCount) })
            .OrderByDescending(x => x.Count)
            .Take(TopProductsLimit)
            .Join(_context.Products.AsNoTracking(), x => x.ProductId, p => p.Id, (x, p) => new ProductAnalyticsSummaryDto(p.Id, p.Name, x.Count))
            .ToListAsync(cancellationToken);

        // Cancelled orders excluded — same "not real business" convention as Story 7.4's
        // dashboard revenue metrics.
        var bestSelling = await _context.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Order.Created >= sevenDaysAgo && oi.Order.Status != OrderStatus.Cancelled)
            .GroupBy(oi => oi.ProductId)
            .Select(g => new { ProductId = g.Key, Count = g.Sum(oi => oi.Quantity) })
            .OrderByDescending(x => x.Count)
            .Take(TopProductsLimit)
            .Join(_context.Products.AsNoTracking(), x => x.ProductId, p => p.Id, (x, p) => new ProductAnalyticsSummaryDto(p.Id, p.Name, x.Count))
            .ToListAsync(cancellationToken);

        var result = new TopProductsDto(mostViewed, bestSelling);

        await _cache.SetAsync(TopProductsCacheKey, result, TopProductsTtl, cancellationToken);

        return result;
    }

    public async Task<List<LowStockProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        // AC #4: no caching anywhere in this method — always read directly from the database.
        // EditUrl is a placeholder route — see Story 7.5's Dev Notes (no admin frontend exists).
        var baseUrl = (_configuration["Frontend:BaseUrl"] ?? string.Empty).TrimEnd('/');

        return await _context.Products
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.Stock != null && p.Stock.Quantity <= p.Stock.AlertThreshold)
            .Select(p => new LowStockProductDto(p.Id, p.Name, p.Stock!.Quantity, p.Stock.AlertThreshold, $"{baseUrl}/admin/produits/{p.Id}"))
            .ToListAsync(cancellationToken);
    }
}
