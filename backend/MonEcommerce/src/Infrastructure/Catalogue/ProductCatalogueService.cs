using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Utilities;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

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

    private const int MaxSuggestions = 5;

    private const int MaxSimilarProducts = 4;

    private static readonly TimeSpan EntryTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromDays(1);

    private readonly IApplicationDbContext _context;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;

    public ProductCatalogueService(IApplicationDbContext context, ICacheService cache, IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
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

        var hasSearch = !string.IsNullOrWhiteSpace(filter.Search);
        if (hasSearch)
        {
            var term = filter.Search!.Trim().ToLowerInvariant();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var withIncludes = query
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Stock);

        // No literal PostgreSQL to_tsvector/to_tsquery (or SQL Server CONTAINSTABLE, which needs
        // an unverifiable-in-this-environment Full-Text Index + raw SQL) — a translatable,
        // boolean-ordered relevance heuristic instead: exact name match, then name-starts-with,
        // then name-contains, ranks above description-only matches, which fall through to the
        // alphabetical tiebreaker. EF Core translates bool-valued OrderBy keys to `CASE WHEN`
        // on SQL Server, and the InMemory provider evaluates them as plain bool comparisons —
        // both providers order identically, so this is fully covered by ProductCatalogueServiceTests.
        var orderedQuery = hasSearch
            ? OrderByRelevance(withIncludes, filter.Search!.Trim().ToLowerInvariant())
            : withIncludes.OrderBy(p => p.Name).ThenBy(p => p.Id);

        var products = await orderedQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = products.Select(MapToSummary).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var result = new PagedProductsResult<ProductSummaryDto>(items, totalCount, pageNumber, pageSize, totalPages);

        await _cache.SetAsync(cacheKey, result, EntryTtl, cancellationToken);

        return result;
    }

    public async Task<ProductDetailDto> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var version = await GetCatalogueVersionAsync(cancellationToken);
        var cacheKey = $"catalogue:v{version}:product:{id}";

        var cached = await _cache.GetAsync<ProductDetailDto>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var product = await _context.Products.AsNoTracking()
            .Where(p => p.IsPublished)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Product), id);

        var result = MapToDetail(product);

        await _cache.SetAsync(cacheKey, result, EntryTtl, cancellationToken);

        return result;
    }

    public async Task<SuggestionsResult> GetSearchSuggestionsAsync(string term, CancellationToken cancellationToken = default)
    {
        var normalized = term.Trim().ToLowerInvariant();

        var categories = await _context.Categories.AsNoTracking()
            .Where(c => c.Name.ToLower().Contains(normalized))
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => c.Name)
            .Take(MaxSuggestions)
            .ToListAsync(cancellationToken);

        // "Up to 5 suggestions (categories + product names)" — categories fill first, products
        // fill whatever's left, so the combined total never exceeds MaxSuggestions.
        var remaining = MaxSuggestions - categories.Count;
        var products = remaining == 0
            ? []
            : await _context.Products.AsNoTracking()
                .Where(p => p.IsPublished && p.Name.ToLower().Contains(normalized))
                .OrderBy(p => p.Name)
                .ThenBy(p => p.Id)
                .Select(p => p.Name)
                .Take(remaining)
                .ToListAsync(cancellationToken);

        return new SuggestionsResult(categories, products);
    }

    public async Task<List<CategorySummaryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var version = await GetCatalogueVersionAsync(cancellationToken);
        var cacheKey = $"catalogue:v{version}:categories";

        var cached = await _cache.GetAsync<List<CategorySummaryDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var categories = await _context.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategorySummaryDto(c.Id, c.Name, c.Slug))
            .ToListAsync(cancellationToken);

        await _cache.SetAsync(cacheKey, categories, EntryTtl, cancellationToken);

        return categories;
    }

    public async Task<List<ProductSummaryDto>> GetSimilarProductsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var version = await GetCatalogueVersionAsync(cancellationToken);
        var cacheKey = $"catalogue:v{version}:similar:{productId}";

        var cached = await _cache.GetAsync<List<ProductSummaryDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var categoryId = await _context.Products.AsNoTracking()
            .Where(p => p.Id == productId && p.IsPublished)
            .Select(p => (Guid?)p.CategoryId)
            .FirstOrDefaultAsync(cancellationToken);

        // A missing/unpublished source product just means "nothing to show here" — not a
        // 404-worthy request the way GetProductByIdAsync treats it (this is a secondary section
        // on the page, not the page's own primary resource).
        if (categoryId == null)
        {
            return [];
        }

        var similarProducts = await _context.Products.AsNoTracking()
            .Where(p => p.IsPublished && p.CategoryId == categoryId && p.Id != productId)
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Stock)
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Take(MaxSimilarProducts)
            .ToListAsync(cancellationToken);

        var result = similarProducts.Select(MapToSummary).ToList();

        await _cache.SetAsync(cacheKey, result, EntryTtl, cancellationToken);

        return result;
    }

    public async Task<List<SitemapEntryDto>> GetSitemapEntriesAsync(CancellationToken cancellationToken = default)
    {
        // A bare `!` here would let a missing/misconfigured Frontend:BaseUrl throw a bare
        // NullReferenceException out of a public, unauthenticated, crawler-facing endpoint (a raw
        // 500 with no useful diagnostic) — or, if the key exists but is an empty string, silently
        // produce scheme-less/host-less <loc> URLs that violate the sitemap protocol's
        // fully-qualified-URL requirement without failing at all. Failing loudly and specifically
        // here is more useful to whoever operates this in production than either of those.
        var baseUrl = _configuration["Frontend:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl configuration is required to build sitemap URLs but is missing or empty.");
        }
        baseUrl = baseUrl.TrimEnd('/');

        var version = await GetCatalogueVersionAsync(cancellationToken);
        var cacheKey = $"catalogue:v{version}:sitemap";

        var cached = await _cache.GetAsync<List<SitemapEntryDto>>(cacheKey, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        var products = await _context.Products.AsNoTracking()
            .Where(p => p.IsPublished)
            .Include(p => p.Category)
            .ToListAsync(cancellationToken);

        var result = products
            .Select(p => new SitemapEntryDto(
                $"{baseUrl}/catalogue/{p.Category.Slug}/{SlugHelper.Slugify(p.Name)}-{p.Id}",
                p.LastModified))
            .ToList();

        // Same versioned cache scheme as every other catalogue read — GetSitemapEntriesAsync was
        // previously the one uncached read path, meaning every crawler hit forced a full
        // Where(IsPublished).Include(Category) table scan with no TTL protection at all.
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
        // Search is normalized (trimmed + lower-cased) before hashing, same as the value actually
        // used to build the WHERE clause below — otherwise "Cuir", "cuir", " cuir ", and null/""/
        // "  " (all functionally identical queries) each hash to a distinct cache key, needlessly
        // fragmenting the cache for exactly the traffic pattern a search endpoint sees most.
        var normalizedFilter = filter with { PageNumber = pageNumber, PageSize = pageSize, Search = NormalizeSearch(filter.Search) };
        var canonical = JsonSerializer.Serialize(normalizedFilter);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return $"catalogue:v{version}:products:{hash}";
    }

    private static string? NormalizeSearch(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : search.Trim().ToLowerInvariant();

    private static IOrderedQueryable<Product> OrderByRelevance(IQueryable<Product> query, string term) => query
        .OrderByDescending(p => p.Name.ToLower() == term)
        .ThenByDescending(p => p.Name.ToLower().StartsWith(term))
        .ThenByDescending(p => p.Name.ToLower().Contains(term))
        .ThenBy(p => p.Name)
        .ThenBy(p => p.Id);

    private static ProductDetailDto MapToDetail(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.PriceInCents,
        product.Material,
        product.Color,
        product.Dimensions,
        product.Stock?.Quantity ?? 0,
        (product.Stock?.Quantity ?? 0) > 0,
        product.CategoryId,
        product.Category.Name,
        product.Category.Slug,
        product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).ToList());

    private static ProductSummaryDto MapToSummary(Product product) => new(
        product.Id,
        product.Name,
        product.PriceInCents,
        product.Material,
        product.Color,
        product.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault()?.Url,
        product.CategoryId,
        product.Category.Name,
        product.Category.Slug,
        (product.Stock?.Quantity ?? 0) > 0);
}
