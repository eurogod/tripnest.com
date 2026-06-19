using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Reviews;

public class ReviewResponse
{
    public required string ReviewId { get; set; }
    public required string ReviewerId { get; set; }
    public required string RevieweeId { get; set; }
    public required string PropertyId { get; set; }
    public int Rating { get; set; }
    public required string Comment { get; set; }
    public ReviewType Type { get; set; }
    public DateTime CreatedAt { get; set; }
}
