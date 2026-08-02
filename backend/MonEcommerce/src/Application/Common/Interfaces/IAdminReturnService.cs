using MonEcommerce.Application.Returns.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IAdminReturnService
{
    Task<List<AdminReturnSummaryDto>> GetReturnsAsync(AdminReturnFilter filter, CancellationToken cancellationToken = default);
}
