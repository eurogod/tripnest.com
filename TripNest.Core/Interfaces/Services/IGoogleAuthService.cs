using TripNest.Core.DTOs.Auth;

namespace TripNest.Core.Interfaces.Services;

public interface IGoogleAuthService
{
    /// <summary>True when a Google client id is configured (social sign-in enabled).</summary>
    bool IsConfigured { get; }

    /// <summary>Validates the ID token and returns the verified identity, or null if invalid/unconfigured.</summary>
    Task<GoogleUserInfo?> ValidateAsync(string idToken);
}
