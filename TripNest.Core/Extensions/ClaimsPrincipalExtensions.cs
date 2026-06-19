using System.Security.Claims;

namespace TripNest.Core.Extensions;

/// <summary>
/// Extension methods for ClaimsPrincipal to handle JWT claim mapping.
/// ASP.NET Core's JWT Bearer middleware with MapInboundClaims=true (default)
/// automatically maps JWT's standard "sub" claim to ClaimTypes.NameIdentifier.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Get the user ID from claims, handling both unmapped and mapped claim types.
    /// Tries "sub" first (for backward compatibility), then falls back to NameIdentifier.
    /// Null-safe so it can be called on a possibly-unauthenticated principal.
    /// </summary>
    public static string? GetUserId(this ClaimsPrincipal? principal)
    {
        return principal?.FindFirst("sub")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
