namespace MonEcommerce.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(string userId, string email, IList<string> roles);
    string GenerateRefreshToken();
}
