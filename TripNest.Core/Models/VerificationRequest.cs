using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class VerificationRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string GhanaCardNumber { get; set; }
    public required string SelfiePhotoPath { get; set; }
    public required string NiaPhotoUrl { get; set; }

    // Claimed identity captured at submission so the background processor can run the
    // authority cross-check (and resume after a restart) without the original request body.
    public string? ClaimedFirstName { get; set; }
    public string? ClaimedLastName { get; set; }
    public DateOnly? ClaimedDateOfBirth { get; set; }

    public double? FaceMatchScore { get; set; }

    // Anti-spoofing/liveness score (0-100) for the submitted selfie, returned by the
    // face-match sidecar. Persisted alongside the match score for audit and support review.
    public double? LivenessScore { get; set; }

    public string? FailureReason { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.NotStarted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
