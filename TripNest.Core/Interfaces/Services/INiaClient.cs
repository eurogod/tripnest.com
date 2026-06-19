namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// Result of looking up a Ghana Card (TripNest ID) against the TripNest.Id authority service.
/// </summary>
public record NiaVerificationResult
{
    public bool IsValid { get; init; }

    /// <summary>Absolute URL to the citizen's reference photo, fetchable by the face-match sidecar.</summary>
    public string? PhotoUrl { get; init; }

    public string? FullName { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    /// <summary>Card status reported by the authority (e.g. Active, Expired, Revoked, NotFound).</summary>
    public string Status { get; init; } = string.Empty;
}

public interface INiaClient
{
    /// <summary>
    /// Verifies a Ghana Card / TripNest ID number against the TripNest.Id authority service.
    /// </summary>
    Task<NiaVerificationResult> VerifyGhanaCardAsync(string cardId);
}
