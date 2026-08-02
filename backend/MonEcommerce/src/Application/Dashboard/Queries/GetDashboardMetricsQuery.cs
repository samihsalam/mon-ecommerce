using MonEcommerce.Application.Common.Models;
using MonEcommerce.Application.Common.Security;
using MonEcommerce.Domain.Constants;

namespace MonEcommerce.Application.Dashboard.Queries;

[Authorize(Roles = Roles.Administrator)]
public record GetDashboardMetricsQuery : IRequest<DashboardMetricsDto>;
