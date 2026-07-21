using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

public record GetSitemapEntriesQuery : IRequest<List<SitemapEntryDto>>;
