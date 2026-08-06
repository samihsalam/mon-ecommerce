using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Account.Models;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Enums;

namespace MonEcommerce.Application.Account.Queries;

public class GetAccountDeletionRequestsQueryHandler : IRequestHandler<GetAccountDeletionRequestsQuery, List<AccountDeletionRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;

    public GetAccountDeletionRequestsQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    {
        _context = context;
        _identityService = identityService;
    }

    public async Task<List<AccountDeletionRequestDto>> Handle(GetAccountDeletionRequestsQuery request, CancellationToken cancellationToken)
    {
        // Oldest first — an admin naturally works the 30-day-window queue in FIFO order.
        var pending = await _context.AccountDeletionRequests
            .Where(r => r.Status == AccountDeletionStatus.Pending)
            .OrderBy(r => r.Created)
            .ToListAsync(cancellationToken);

        var results = new List<AccountDeletionRequestDto>(pending.Count);
        foreach (var r in pending)
        {
            var email = await _identityService.GetEmailAsync(r.UserId) ?? string.Empty;
            results.Add(new AccountDeletionRequestDto(r.Id, r.UserId, email, r.Created));
        }

        return results;
    }
}
