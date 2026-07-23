namespace MonEcommerce.Application.Carts.Models;

// Exactly one of UserId/SessionId must be set — enforced by the factory methods below, not the
// public constructor, so a caller can never accidentally construct an ambiguous or ownerless
// CartOwner (a genuinely important invariant: every cart lookup/mutation in CartService is scoped
// by this value, so an ambiguous owner would be a real IDOR-adjacent bug, not just a data-quality one).
public record CartOwner
{
    public string? UserId { get; }
    public string? SessionId { get; }

    private CartOwner(string? userId, string? sessionId)
    {
        UserId = userId;
        SessionId = sessionId;
    }

    public static CartOwner ForUser(string userId) => new(userId, null);

    public static CartOwner ForSession(string sessionId) => new(null, sessionId);

    public bool IsAuthenticated => UserId != null;
}
