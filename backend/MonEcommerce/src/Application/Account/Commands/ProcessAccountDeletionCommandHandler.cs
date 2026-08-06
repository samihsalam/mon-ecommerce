using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonEcommerce.Application.Common.Exceptions;
using MonEcommerce.Application.Common.Interfaces;
using MonEcommerce.Domain.Enums;
using AppNotFoundException = MonEcommerce.Application.Common.Exceptions.NotFoundException;

namespace MonEcommerce.Application.Account.Commands;

public class ProcessAccountDeletionCommandHandler : IRequestHandler<ProcessAccountDeletionCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IIdentityService _identityService;
    private readonly IPaymentService _paymentService;
    private readonly TimeProvider _timeProvider;
    private readonly IUser _user;
    private readonly ILogger<ProcessAccountDeletionCommandHandler> _logger;

    public ProcessAccountDeletionCommandHandler(
        IApplicationDbContext context,
        IIdentityService identityService,
        IPaymentService paymentService,
        TimeProvider timeProvider,
        IUser user,
        ILogger<ProcessAccountDeletionCommandHandler> logger)
    {
        _context = context;
        _identityService = identityService;
        _paymentService = paymentService;
        _timeProvider = timeProvider;
        _user = user;
        _logger = logger;
    }

    public async Task Handle(ProcessAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        var deletionRequest = await _context.AccountDeletionRequests
            .FirstOrDefaultAsync(r => r.Id == request.RequestId, cancellationToken)
            ?? throw new AppNotFoundException(nameof(Domain.Entities.AccountDeletionRequest), request.RequestId);

        if (deletionRequest.Status != AccountDeletionStatus.Pending)
        {
            throw new ConflictException("Cette demande a déjà été traitée.");
        }

        var originalEmail = await _identityService.GetEmailAsync(deletionRequest.UserId) ?? string.Empty;
        var anonymizedEmail = BuildAnonymizedEmail(originalEmail, deletionRequest.UserId);

        // AC #2 (name/email) and AC #5 (old credentials stop working — login resolves by
        // email/username, so this alone is what makes AC #5 true) in one call.
        var anonymizeResult = await _identityService.AnonymizeUserAsync(deletionRequest.UserId, "Utilisateur supprimé", anonymizedEmail);
        if (!anonymizeResult.Succeeded)
        {
            throw new ConflictException(string.Join(" ", anonymizeResult.Errors));
        }

        // Additional hardening beyond AC #5's literal "cannot log in": an already-issued refresh
        // token on another device must not silently keep working after a right-to-erasure request.
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == deletionRequest.UserId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = _timeProvider.GetUtcNow();
        }

        // AC #2's "address → removed" cannot mean a hard row delete: Order.ShippingAddressId has
        // OnDelete(DeleteBehavior.Restrict) to Address (see OrderConfiguration.cs), and AC #3
        // requires order records to be RETAINED — a customer with any order history would hit an
        // FK-constraint DbUpdateException on SaveChangesAsync, aborting this entire handler AFTER
        // the identity anonymization above already committed (review finding: verified against
        // OrderConfiguration.cs's actual Restrict behavior). Scrubbing the address content in
        // place — same technique as the name/email anonymization above — removes every personal
        // identifier while the row (and any Order referencing it) stays intact, satisfying AC #2
        // and AC #3 together with no FK conflict.
        var addresses = await _context.Addresses
            .Where(a => a.UserId == deletionRequest.UserId)
            .ToListAsync(cancellationToken);
        foreach (var address in addresses)
        {
            address.Street = "Adresse supprimée";
            address.City = string.Empty;
            address.PostalCode = string.Empty;
            address.Country = string.Empty;
        }

        // AC #6 — best-effort; a Stripe-side failure must not roll back the anonymization already
        // performed above.
        try
        {
            await _paymentService.DeleteCustomerDataAsync(originalEmail, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MonEcommerce: failed to request Stripe customer data deletion for account deletion request {RequestId}", deletionRequest.Id);
        }

        deletionRequest.Status = AccountDeletionStatus.Processed;
        deletionRequest.ProcessedByAdminUserId = _user.Id;
        deletionRequest.ProcessedAt = _timeProvider.GetUtcNow();

        await _context.SaveChangesAsync(cancellationToken);
    }

    // RFC 2606 reserves the ".invalid" TLD for "not a real, deliverable address" — exactly this
    // use. Salted with the user's own id so two customers who once shared an email pattern can
    // never collide on the same anonymized address.
    private static string BuildAnonymizedEmail(string originalEmail, string userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(originalEmail + userId));
        var hex = Convert.ToHexString(hash)[..32].ToLowerInvariant();
        return $"{hex}@deleted.invalid";
    }
}
