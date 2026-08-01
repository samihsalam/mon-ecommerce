using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Catalogue.Commands;

public class AddProductImageCommandHandler : IRequestHandler<AddProductImageCommand, ProductImageDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IProductCatalogueService _catalogueService;

    public AddProductImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IProductCatalogueService catalogueService)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _catalogueService = catalogueService;
    }

    public async Task<ProductImageDto> Handle(AddProductImageCommand request, CancellationToken cancellationToken)
    {
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && !p.IsDeleted, cancellationToken);
        if (!productExists)
        {
            throw new AppNotFoundException(nameof(Product), request.ProductId);
        }

        var nextDisplayOrder = await _context.ProductImages
            .Where(i => i.ProductId == request.ProductId)
            .CountAsync(cancellationToken);

        var uploadResult = await _fileStorageService.UploadAsync(
            request.File.Content,
            request.File.FileName,
            "products",
            ImageTransformPreset.ProductGallery,
            cancellationToken);

        var image = new ProductImage
        {
            Id = Guid.NewGuid(),
            ProductId = request.ProductId,
            Url = uploadResult.Url,
            PublicId = uploadResult.PublicId,
            DisplayOrder = nextDisplayOrder,
        };

        _context.ProductImages.Add(image);
        await _context.SaveChangesAsync(cancellationToken);

        // The product may already be published — its cached ProductDetailDto's ImageUrls must
        // not stay stale.
        await _catalogueService.InvalidateCatalogueCacheAsync(cancellationToken);

        return new ProductImageDto(image.Id, image.Url, image.DisplayOrder);
    }
}
