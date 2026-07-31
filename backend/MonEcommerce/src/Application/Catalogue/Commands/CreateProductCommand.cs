using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

[Authorize(Roles = Roles.Administrator)]
public record CreateProductCommand(
    string Name,
    string Description,
    int PriceInCents,
    Guid CategoryId,
    string? Material,
    string? Color,
    string? Dimensions,
    int InitialStock) : IRequest<Guid>;
