namespace TripNest.Core.DTOs.Auth;

/// <summary>The Google ID token (JWT credential) obtained by the browser's Google Sign-In flow.</summary>
public class GoogleSignInRequest
{
    public required string IdToken { get; set; }
}

/// <summary>Verified identity extracted from a Google ID token.</summary>
public record GoogleUserInfo(string Email, string FullName, bool EmailVerified);
