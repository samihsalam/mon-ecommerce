using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

[Authorize(Roles = Roles.Administrator)]
public record AddProductImageCommand(Guid ProductId, ProductImageUpload File) : IRequest<ProductImageDto>;
