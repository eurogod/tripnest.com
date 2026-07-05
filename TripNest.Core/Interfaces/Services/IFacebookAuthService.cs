using TripNest.Core.DTOs.Auth;

namespace TripNest.Core.Interfaces.Services;

public interface IFacebookAuthService
{
    /// <summary>True when a Facebook app id + secret are configured (Facebook sign-in enabled).</summary>
    bool IsConfigured { get; }

    /// <summary>Validates the access token against the Graph API and returns the identity,
    /// or null if invalid/unconfigured.</summary>
    Task<FacebookUserInfo?> ValidateAsync(string accessToken);
}
