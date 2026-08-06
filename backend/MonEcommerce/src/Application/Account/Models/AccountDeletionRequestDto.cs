namespace MonEcommerce.Application.Account.Models;

// Email included (review finding): with no admin UI (ops tooling only, see story Dev Notes), an
// admin needs a way to identify whose data they're about to irreversibly anonymize without a
// separate lookup per UserId.
public record AccountDeletionRequestDto(Guid Id, string UserId, string Email, DateTimeOffset RequestedAt);
