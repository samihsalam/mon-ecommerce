namespace MonEcommerce.Application.Auth.Models;

public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
