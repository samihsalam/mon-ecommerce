using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

public record GetSearchSuggestionsQuery(string? Search) : IRequest<SuggestionsResult>;
