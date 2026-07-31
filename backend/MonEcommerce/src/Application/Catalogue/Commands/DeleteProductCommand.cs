using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

[Authorize(Roles = Roles.Administrator)]
public record DeleteProductCommand(Guid Id) : IRequest;
