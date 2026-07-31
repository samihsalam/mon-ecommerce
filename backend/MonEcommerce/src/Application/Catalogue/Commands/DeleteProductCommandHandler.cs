using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class DeleteProductCommandHandler : IRequestHandler<DeleteProductCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public DeleteProductCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        // Looked up without an !IsDeleted filter (unlike UpdateProductCommandHandler) so a
        // retried DELETE on an already-deleted product is a no-op success, not a 404 — standard
        // DELETE idempotency.
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Product), request.Id);

        if (product.IsDeleted)
        {
            return;
        }

        product.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        // AC #3: "hidden from catalogue" — if the product was published and cached, its cache
        // entries must be dropped, same reasoning as UpdateProductCommandHandler's invalidation.
        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);
    }
}
