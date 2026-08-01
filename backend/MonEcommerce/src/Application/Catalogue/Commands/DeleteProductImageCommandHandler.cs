using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class DeleteProductImageCommandHandler : IRequestHandler<DeleteProductImageCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IProductCatalogueService _catalogueService;

    public DeleteProductImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IProductCatalogueService catalogueService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _catalogueService = catalogueService;
    }

    public async Task Handle(DeleteProductImageCommand request, CancellationToken cancellationToken)
    {
        // Scoped by both ProductId and ImageId in the query itself — a stray imageId under the
        // wrong product 404s rather than silently deleting it (IDOR-prevention convention).
        var image = await _context.ProductImages
            .FirstOrDefaultAsync(i => i.Id == request.ImageId && i.ProductId == request.ProductId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(ProductImage), request.ImageId);

        // Deletes the Cloudinary asset before touching the DB row — if this throws, nothing
        // changes, keeping Cloudinary and the database from drifting out of sync.
        await _fileStorageService.DeleteAsync(image.PublicId, cancellationToken);

        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync(cancellationToken);

        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);
    }
}
