using System.ComponentModel.DataAnnotations;

namespace TripNest.Core.DTOs.Reviews;

public class CreateReviewRequest
{
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    [Range(1, 5)]
    public int Rating { get; set; }
    [StringLength(2000)]
    public string? Comment { get; set; }
}
