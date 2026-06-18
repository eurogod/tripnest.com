namespace TripNest.Core.Interfaces.Services;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(string userId, string email, string fullName, string role);

    Task<string> GenerateRefreshTokenAsync();

    Task<System.Security.Claims.ClaimsPrincipal?> ValidateTokenAsync(string token);
}
