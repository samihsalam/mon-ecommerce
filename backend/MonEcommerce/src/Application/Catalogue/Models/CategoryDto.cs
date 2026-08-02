namespace MonEcommerce.Application.Catalogue.Models;

public record CategoryDto(Guid Id, string Name, string Slug, Guid? ParentId);
