using Microsoft.EntityFrameworkCore;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Entities;
using MonEcommerce.Domain.Enums;
using MonEcommerce.Domain.Events;

namespace MonEcommerce.Application.Account.Commands;

public class RequestAccountDeletionCommandHandler : IRequestHandler<RequestAccountDeletionCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IUser _user;

    public RequestAccountDeletionCommandHandler(IApplicationDbContext context, IIdentityService identityService, IUser user)
    {
        _context = context;
        _identityService = identityService;
        _user = user;
    }

    public async Task<Guid> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        // Idempotency guard — a customer already has a pending request, don't create a duplicate.
        var alreadyPending = await _context.AccountDeletionRequests.AnyAsync(
            r => r.UserId == _user.Id! && r.Status == AccountDeletionStatus.Pending,
            cancellationToken);

        if (alreadyPending)
        {
            throw new ConflictException("Une demande de suppression est déjà en cours pour ce compte.");
        }

        var deletionRequest = new AccountDeletionRequest
        {
            Id = Guid.NewGuid(),
            UserId = _user.Id!,
            Status = AccountDeletionStatus.Pending,
        };

        var customerEmail = await _identityService.GetEmailAsync(_user.Id!);
        deletionRequest.AddDomainEvent(new AccountDeletionRequestedEvent(deletionRequest.Id, customerEmail ?? string.Empty));

        _context.AccountDeletionRequests.Add(deletionRequest);
        await _context.SaveChangesAsync(cancellationToken);

        return deletionRequest.Id;
    }
}
