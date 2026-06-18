namespace TripNest.Core.DTOs.TrustScore;

public class StayFeedbackRequest
{
    public required string BookingId { get; set; }
    public int AccuracyRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int SafetyRating { get; set; }
    public string? Comment { get; set; }
}
