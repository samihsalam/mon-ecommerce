using MonEcommerce.Application.Common.Models;

namespace MonEcommerce.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<string?> GetUserNameAsync(string userId);

    // Needed for Story 4.6's webhook handler — the customer's email for order-confirmation/
    // stock-unavailable notifications, resolved from the Stripe PaymentIntent metadata's userId
    // (there's no authenticated IUser/claims principal for a Stripe-initiated webhook request).
    Task<string?> GetEmailAsync(string userId);

    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string policyName);

    Task<(Result Result, string UserId)> CreateUserAsync(string userName, string password);

    Task<Result> DeleteUserAsync(string userId);

    // Story 8.3, AC #2/#5: overwrites Name/Email/UserName in place — the Application layer has no
    // reference to ASP.NET Identity/UserManager (Clean Architecture boundary), so
    // ProcessAccountDeletionCommandHandler goes through this rather than touching UserManager
    // directly, same as DeleteUserAsync already wraps UserManager.DeleteAsync. Changing the email
    // is also what makes AC #5 ("cannot log in with old credentials") true — login resolves users
    // by email/username, so the old credentials stop resolving to this account once this returns
    // successfully.
    // No CancellationToken param — same as every other method on this interface, since the
    // underlying UserManager calls (SetEmailAsync/SetUserNameAsync/FindByIdAsync) don't accept one.
    Task<Result> AnonymizeUserAsync(string userId, string anonymizedName, string anonymizedEmail);
}
