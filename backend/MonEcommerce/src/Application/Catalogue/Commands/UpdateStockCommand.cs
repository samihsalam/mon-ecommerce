using MonEcommerce.Application.Catalogue.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Catalogue.Commands;

// Sets an absolute new stock level (and alert threshold), not a relative adjustment — matches the
// AC's own { quantity, alertThreshold } request shape. Reason is optional; the handler falls back
// to "Ajustement manuel" so the logged StockMovement's Reason is never blank.
[Authorize(Roles = Roles.Administrator)]
public record UpdateStockCommand(Guid ProductId, int Quantity, int AlertThreshold, string? Reason) : IRequest<StockDto>;
