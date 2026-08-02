namespace MonEcommerce.Application.Catalogue.Models;

public record TopProductsDto(List<ProductAnalyticsSummaryDto> MostViewed, List<ProductAnalyticsSummaryDto> BestSelling);
