using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class ReorderProductImagesCommandHandler : IRequestHandler<ReorderProductImagesCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public ReorderProductImagesCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task Handle(ReorderProductImagesCommand request, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);
        if (!productExists)
        {
            throw new AppNotFoundException(nameof(Product), request.ProductId);
        }

        var images = await _context.ProductImages
            .Where(i => i.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);

        // The submitted id set must exactly match the product's existing images — a mismatched,
        // partial, or foreign id is a client error (422), not a 404 (the product itself exists)
        // or a silent partial reorder.
        var existingIds = images.Select(i => i.Id).ToHashSet();
        var requestedIds = request.ImageIds.ToHashSet();
        if (!existingIds.SetEquals(requestedIds))
        {
            throw new AppValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    nameof(request.ImageIds),
                    "La liste d'images fournie ne correspond pas exactement aux images existantes du produit."),
            ]);
        }

        var imagesById = images.ToDictionary(i => i.Id);
        for (var index = 0; index < request.ImageIds.Count; index++)
        {
            imagesById[request.ImageIds[index]].DisplayOrder = index;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);
    }
}
