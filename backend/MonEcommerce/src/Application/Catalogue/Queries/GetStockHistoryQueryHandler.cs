using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetStockHistoryQueryHandler : IRequestHandler<GetStockHistoryQuery, List<StockMovementDto>>
{
    private readonly IApplicationDbContext _context;

    public GetStockHistoryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    // AC #6: reads directly from PostgreSQL, no Redis involved anywhere in this path.
    public Task<List<StockMovementDto>> Handle(GetStockHistoryQuery request, CancellationToken cancellationToken) =>
        _context.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == request.ProductId)
            .OrderByDescending(m => m.Created)
            .Select(m => new StockMovementDto(m.Id, m.PreviousQuantity, m.NewQuantity, m.Reason, m.CreatedBy, m.Created))
            .ToListAsync(cancellationToken);
}
