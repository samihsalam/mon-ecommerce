using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

// Deliberately excludes IsPublished (Story 6.5's own PATCH /publish endpoint) and stock quantity
// (Story 6.4's PATCH /stock endpoint) — see Story 6.1's Dev Notes.
[Authorize(Roles = Roles.Administrator)]
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    int PriceInCents,
    Guid CategoryId,
    string? Material,
    string? Color,
    string? Dimensions) : IRequest<AdminProductDto>;
