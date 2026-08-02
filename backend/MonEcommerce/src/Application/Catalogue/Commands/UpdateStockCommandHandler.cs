using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Events;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class UpdateStockCommandHandler : IRequestHandler<UpdateStockCommand, StockDto>
{
    private const string DefaultReason = "Ajustement manuel";

    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public UpdateStockCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task<StockDto> Handle(UpdateStockCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Product), request.ProductId);

        var stock = product.Stock ?? throw new AppNotFoundException(nameof(Stock), request.ProductId);

        var previousQuantity = stock.Quantity;
        stock.Quantity = request.Quantity;
        stock.AlertThreshold = request.AlertThreshold;

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? DefaultReason : request.Reason;

        _context.StockMovements.Add(new StockMovement
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            PreviousQuantity = previousQuantity,
            NewQuantity = request.Quantity,
            Reason = reason,
        });

        // AC #2: an alert event, raised only when the new level is at or below the threshold —
        // not a general "stock changed" event fired on every update.
        if (request.Quantity <= request.AlertThreshold)
        {
            stock.AddDomainEvent(new StockUpdatedEvent(product.Id, product.Name, request.Quantity, request.AlertThreshold));
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Mitigates the pre-existing tension between AC #6 ("stock never cached") and Story 3.x's
        // ProductCatalogueService, which does cache StockQuantity/InStock as part of the product
        // DTO — see this story's Dev Notes.
        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);

        return new StockDto(product.Id, stock.Quantity, stock.AlertThreshold);
    }
}
