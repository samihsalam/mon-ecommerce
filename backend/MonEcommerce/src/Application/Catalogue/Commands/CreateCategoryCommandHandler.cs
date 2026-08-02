using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Application.Common.Utilities;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, CategoryDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IProductCatalogueService _catalogueService;

    public CreateCategoryCommandHandler(IApplicationDbContext context, IProductCatalogueService catalogueService)
    {
        _context = context;
        _catalogueService = catalogueService;
    }

    public async Task<CategoryDto> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentId.HasValue)
        {
            var parentExists = await _context.Categories.AnyAsync(c => c.Id == request.ParentId, cancellationToken);
            if (!parentExists)
            {
                throw new AppNotFoundException(nameof(Category), request.ParentId.Value);
            }
        }

        // AC #6: kebab-case ("Sacs Mode" -> "sacs-mode") — the same slugify already used for
        // product URLs/sitemap entries (Story 3.5), not a second implementation.
        var slug = SlugHelper.Slugify(request.Name);

        var slugExists = await _context.Categories.AnyAsync(c => c.Slug == slug, cancellationToken);
        if (slugExists)
        {
            throw new ConflictException($"Une catégorie avec le slug '{slug}' existe déjà.");
        }

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = slug,
            ParentId = request.ParentId,
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        // GetCategoriesAsync's result (the public GET /categories filter list, AC #5) is cached —
        // without this, a newly created category wouldn't appear until the 5-minute TTL expires.
        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);

        return new CategoryDto(category.Id, category.Name, category.Slug, category.ParentId);
    }
}
