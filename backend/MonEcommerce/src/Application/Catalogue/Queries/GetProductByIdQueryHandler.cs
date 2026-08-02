using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IProductCatalogueService _catalogueService;
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public GetProductByIdQueryHandler(IProductCatalogueService catalogueService, IApplicationDbContext context, TimeProvider timeProvider)
    {
        _catalogueService = catalogueService;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ProductDetailDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await _catalogueService.GetProductByIdAsync(request.Id, cancellationToken);

        // Story 7.5, AC #6: incremented here, not inside ProductCatalogueService's own cached
        // read path — a Redis cache hit skips that method's body entirely, which would silently
        // under-count views for a popular (frequently cached) product. The MediatR pipeline
        // always runs this handler regardless of the cache's state, so the counter is accurate
        // for every successful request, not just cache misses. No increment on a 404 — a missing/
        // unpublished product throws out of GetProductByIdAsync above, before this line.
        await IncrementViewCountAsync(request.Id, cancellationToken);

        return result;
    }

    private async Task IncrementViewCountAsync(Guid productId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

        // Read-then-write, not a single atomic upsert — acceptable for a soft, imprecision-
        // tolerant analytics counter (a rare race under heavy concurrent traffic to the same
        // product+day could drop at most one increment); not a pattern used for financial or
        // stock data anywhere in this codebase.
        var row = await _context.ProductDailyViewCounts
            .FirstOrDefaultAsync(v => v.ProductId == productId && v.Date == today, cancellationToken);

        if (row is null)
        {
            _context.ProductDailyViewCounts.Add(new ProductDailyViewCount
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Date = today,
                ViewCount = 1,
            });
        }
        else
        {
            row.ViewCount++;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
