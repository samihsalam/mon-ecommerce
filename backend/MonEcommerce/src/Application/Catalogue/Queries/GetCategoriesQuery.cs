using MonEcommerce.Application.Catalogue.Models;

namespace MonEcommerce.Application.Catalogue.Queries;

public record GetCategoriesQuery : IRequest<List<CategorySummaryDto>>;
