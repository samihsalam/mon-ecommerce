using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;

namespace MonEcommerce.Infrastructure.Catalogue;

public class ProductCatalogueService : IProductCatalogueService
{
    private const string VersionKey = "catalogue:version";

    // Upper-bounds pageNumber so (pageNumber - 1) * pageSize can never overflow 32-bit int
    // arithmetic (pageSize is separately clamped to 100 below) — a crafted request with an
    // extreme pageNumber previously wrapped to a negative Skip() value, which EF Core translates
    // to a negative SQL OFFSET that SQL Server rejects (500, unauthenticated/public endpoint).
    // 1,000,000 pages is comfortably beyond any realistic catalogue size at any pageSize.
    private const int MaxPageNumber = 1_000_000;

    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(1);

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;

    public ProductCatalogueService(IApplicationDbContext context, ICacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<PagedProductsResult<ProductSummaryDto>> GetProductsAsync(ProductFilter filter, CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Clamp(filter.PageNumber, 1, MaxPageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var version = await GetCatalogueVersionAsync(cancellationToken);
        var cacheKey = BuildCacheKey(filter, pageNumber, pageSize, version);

        var cached = await _cache.GetAsync<PagedProductsResult<ProductSummaryDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var query = _context.Products.AsNoTracking().Where(p => p.IsPublished);

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == filter.CategoryId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Material))
        {
            query = query.Where(p => p.Material == filter.Material);
        }

        if (!string.IsNullOrWhiteSpace(filter.Color))
        {
            query = query.Where(p => p.Color == filter.Color);
        }

        if (filter.PriceMin.HasValue)
        {
            query = query.Where(p => p.PriceInCents >= filter.PriceMin);
        }

        if (filter.PriceMax.HasValue)
        {
            query = query.Where(p => p.PriceInCents <= filter.PriceMax);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Stock)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(MapToSummary).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PagedProductsResult<ProductSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);

        await _cache.SetAsync(cacheKey, result, EntryTtl, cancellationToken);

        return result;
    }

    public async Task InvalidateCatalogueCacheAsync(CancellationToken cancellationToken = default)
    {
        // Read-then-increment-then-write, not an atomic Redis INCR — ICacheService's interface
        // has no atomic-increment primitive, and adding one for this alone would be
        // disproportionate. Two concurrent invalidations can race and lose one increment, but
        // since old-version keys are never explicitly deleted (they just become unreachable and
        // expire on their own 5-minute TTL), any successful bump still orphans every entry keyed
        // under the prior version — a lost increment doesn't resurrect stale data, it just means
        // one fewer version number was "spent." Not a functional bug as used here.
        var current = await GetCatalogueVersionAsync(cancellationToken);
        await _cache.SetAsync(VersionKey, current + 1, VersionTtl, cancellationToken);
    }

    private async Task<int> GetCatalogueVersionAsync(CancellationToken cancellationToken)
        => await _cache.GetAsync<int?>(VersionKey, cancellationToken) ?? 1;

    // Material/Color are unvalidated free-text query params — raw delimited string interpolation
    // (e.g. "mat={x}:col={y}") let two DIFFERENT filter combinations collide onto the identical
    // key string whenever a value itself contained a delimiter substring like ":col=" (a crafted
    // Material value could make a request appear identical to a differently-filtered one, serving
    // the wrong cached product list). JSON-serializing the filter tuple and hashing it sidesteps
    // delimiter ambiguity entirely — two distinct filter values always produce a distinct JSON
    // string, regardless of what characters they contain.
    private static string BuildCacheKey(ProductFilter filter, int pageNumber, int pageSize, int version)
    {
        var canonical = JsonSerializer.Serialize(filter with { PageNumber = pageNumber, PageSize = pageSize });
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"catalogue:v{version}:products:{hash}";
    }

    private static ProductSummaryDto MapToSummary(Product product) => new(
        product.Id,
        product.Name,
        product.PriceInCents,
        product.Material,
        product.Color,
        product.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.Url,
        product.CategoryId,
        product.Category.Name,
        (product.Stock?.Quantity ?? 0) > 0);
}
