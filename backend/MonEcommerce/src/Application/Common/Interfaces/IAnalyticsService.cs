using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAnalyticsService
{
    // Cached in Redis, 1-hour TTL (AC #3).
    Task<TopProductsDto> GetTopProductsAsync(CancellationToken cancellationToken = default);

    // Never cached — always read directly from the database (AC #4).
    Task<List<LowStockProductDto>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
}
