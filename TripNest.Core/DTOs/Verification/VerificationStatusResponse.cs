using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Verification;

public class VerificationStatusResponse
{
    public required string VerificationId { get; set; }
    public required string GhanaCardNumber { get; set; }
    public VerificationStatus Status { get; set; }
    public double? FaceMatchScore { get; set; }
    public double? LivenessScore { get; set; }
    public string? FailureReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
