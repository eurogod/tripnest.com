namespace TripNest.Core.DTOs.Auth;

/// <summary>The Facebook user access token obtained by the frontend's Facebook Login flow.</summary>
public class FacebookSignInRequest
{
    public required string AccessToken { get; set; }
}

/// <summary>
/// Verified identity extracted from a Facebook access token. Email is null when the Facebook
/// account has no email (phone-registered accounts) or the user denied the email permission.
/// </summary>
public record FacebookUserInfo(string? Email, string FullName);
