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
}
