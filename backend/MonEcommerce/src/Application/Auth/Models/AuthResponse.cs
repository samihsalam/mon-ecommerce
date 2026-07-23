namespace MonEcommerce.Application.Auth.Models;

// UserId (Story 4.1) — lets the Login endpoint merge an anonymous cart into the just-authenticated
// user's account cart without a redundant lookup; the access token already encodes it as a claim,
// but re-parsing a JWT you just issued to get a value you already had in hand is needless.
public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, string UserId);
