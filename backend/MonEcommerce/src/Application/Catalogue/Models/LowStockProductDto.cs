namespace MonEcommerce.Application.Catalogue.Models;

// EditUrl is a placeholder route (no admin frontend exists anywhere in this codebase yet) — see
// Story 7.5's Dev Notes.
public record LowStockProductDto(Guid ProductId, string Name, int CurrentStock, int AlertThreshold, string EditUrl);
