using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

// The one and only place IsPublished is set from an admin action — Story 6.1's UpdateProductCommand
// deliberately excludes it, naming this command as the owner (see 6.1's Dev Notes).
[Authorize(Roles = Roles.Administrator)]
public record PublishProductCommand(Guid ProductId, bool IsPublished) : IRequest<AdminProductDto>;
