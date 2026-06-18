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
    public double? FaceMatchScore { get; set; }
    public string? FailureReason { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.NotStarted;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}
