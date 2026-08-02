namespace MonEcommerce.Application.Catalogue.Models;

// Story 6.5, AC #2: ParentId lets a filter UI reconstruct category nesting — the DTO was
// previously flat (Id, Name, Slug only).
public record CategorySummaryDto(Guid Id, string Name, string Slug, Guid? ParentId);
