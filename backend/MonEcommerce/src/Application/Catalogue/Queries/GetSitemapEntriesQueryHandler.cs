using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetSitemapEntriesQueryHandler : IRequestHandler<GetSitemapEntriesQuery, List<SitemapEntryDto>>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetSitemapEntriesQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    public Task<List<SitemapEntryDto>> Handle(GetSitemapEntriesQuery request, CancellationToken cancellationToken)
        => _catalogueService.GetSitemapEntriesAsync(cancellationToken);
}
