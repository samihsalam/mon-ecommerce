using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, AdminProductDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public UpdateProductCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task<AdminProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        // A soft-deleted product is inert to admin mutations too, not just public reads — treated
        // as gone, same as a product that never existed.
        var product = await _context.Products
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == request.Id && !p.IsDeleted, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Product), request.Id);

        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new AppNotFoundException(nameof(Category), request.CategoryId);
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.PriceInCents = request.PriceInCents;
        product.CategoryId = request.CategoryId;
        product.Material = request.Material;
        product.Color = request.Color;
        product.Dimensions = request.Dimensions;

        await _context.SaveChangesAsync(cancellationToken);

        // AC #2: the Redis catalogue cache is invalidated — the product may currently be
        // published and cached, so its updated fields must not be served stale.
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
