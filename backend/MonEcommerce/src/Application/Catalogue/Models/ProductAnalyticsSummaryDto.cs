namespace MonEcommerce.Application.Catalogue.Models;

// Count is either the last-7-days view sum or units sold, depending on which list (MostViewed vs
// BestSelling) this appears in — see TopProductsDto.
public record ProductAnalyticsSummaryDto(Guid Id, string Name, int Count);
