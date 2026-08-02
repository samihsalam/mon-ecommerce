using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public DeleteCategoryCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Category), request.Id);

        // Category.ParentId's FK is DeleteBehavior.Restrict (Story 1.3) — a category with
        // children would be rejected by the database anyway; checked here for a clear 409
        // instead of an unhandled DbUpdateException.
        var hasChildren = await _context.Categories.AnyAsync(c => c.ParentId == request.Id, cancellationToken);
        if (hasChildren)
        {
            throw new ConflictException("Impossible de supprimer une catégorie contenant des sous-catégories.");
        }

        // AC #7's own named condition.
        var hasPublishedProducts = await _context.Products
            .AnyAsync(p => p.CategoryId == request.Id && p.IsPublished && !p.IsDeleted, cancellationToken);
        if (hasPublishedProducts)
        {
            throw new ConflictException("Impossible de supprimer une catégorie contenant des produits publiés.");
        }

        // Product.CategoryId's FK is also DeleteBehavior.Restrict — an unpublished-only category
        // would still be rejected by the database without this second, broader check.
        var hasAnyProducts = await _context.Products
            .AnyAsync(p => p.CategoryId == request.Id && !p.IsDeleted, cancellationToken);
        if (hasAnyProducts)
        {
            throw new ConflictException("Impossible de supprimer une catégorie contenant des produits, même non publiés.");
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync(cancellationToken);

        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);
    }
}
