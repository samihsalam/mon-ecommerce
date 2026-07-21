using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetProductByIdQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    public Task<ProductDetailDto> Handle(GetProductByIdQuery request, CancellationToken cancellationToken)
        => _catalogueService.GetProductByIdAsync(request.Id, cancellationToken);
}
