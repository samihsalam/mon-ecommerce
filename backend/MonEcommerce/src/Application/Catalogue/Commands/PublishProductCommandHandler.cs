using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;
using AppValidationException = MonEcommerce.Application.Common.Exceptions.ValidationException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class PublishProductCommandHandler : IRequestHandler<PublishProductCommand, AdminProductDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public PublishProductCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task<AdminProductDto> Handle(PublishProductCommand request, CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Stock)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Product), request.ProductId);

        // Story 6.2's AC #3, implemented here — the endpoint that can actually "try to publish"
        // a product didn't exist until this story.
        if (request.IsPublished && product.Images.Count == 0)
        {
            throw new AppValidationException(
            [
                new FluentValidation.Results.ValidationFailure(
                    nameof(request.IsPublished),
                    "Au moins une image est requise pour publier un produit"),
            ]);
        }

        product.IsPublished = request.IsPublished;
        await _context.SaveChangesAsync(cancellationToken);

        // Both directions invalidate — AC #4 states it explicitly for unpublish; publish needs it
        // too, symmetric with every other catalogue-affecting mutation (Stories 6.1/6.2/6.4),
        // otherwise a cached "products list" from just before the publish wouldn't show it until
        // the 5-minute TTL expires.
        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);

        return new AdminProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.PriceInCents,
            product.Material,
            product.Color,
            product.Dimensions,
            product.CategoryId,
            product.IsPublished,
            product.Stock?.Quantity ?? 0);
    }
}
