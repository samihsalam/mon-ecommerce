using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateProductCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            throw new AppNotFoundException(nameof(Category), request.CategoryId);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            PriceInCents = request.PriceInCents,
            CategoryId = request.CategoryId,
            Material = request.Material,
            Color = request.Color,
            Dimensions = request.Dimensions,
            // AC #1: created "Dépublié" — never visible publicly at creation time.
            IsPublished = false,
        };

        product.Stock = new Stock
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Quantity = request.InitialStock,
        };

        // No cache invalidation here — an unpublished product cannot appear in any cached
        // (published-only) catalogue read, so there is nothing stale to evict.
        _context.Products.Add(product);
        await _context.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
