namespace TripNest.Core.DTOs.Reviews;

public class CreateReviewRequest
{
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
