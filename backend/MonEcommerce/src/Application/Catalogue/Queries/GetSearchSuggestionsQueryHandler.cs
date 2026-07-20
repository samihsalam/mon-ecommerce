using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Interfaces;

namespace MonEcommerce.Application.Catalogue.Queries;

public class GetSearchSuggestionsQueryHandler : IRequestHandler<GetSearchSuggestionsQuery, SuggestionsResult>
{
    private readonly IProductCatalogueService _catalogueService;

    public GetSearchSuggestionsQueryHandler(IProductCatalogueService catalogueService)
    {
        _catalogueService = catalogueService;
    }

    // request.Search is non-null/non-whitespace by the time Handle runs — ValidationBehaviour
    // (ahead of this handler in the MediatR pipeline) rejects anything else via 422.
    public Task<SuggestionsResult> Handle(GetSearchSuggestionsQuery request, CancellationToken cancellationToken)
        => _catalogueService.GetSearchSuggestionsAsync(request.Search!, cancellationToken);
}
