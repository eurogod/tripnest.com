using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Caretakers;

public class CaretakerResponse
{
    public required string CaretakerId { get; set; }
    public required string UserId { get; set; }
    /// <summary>Legacy single-property link; current links come from assignments.</summary>
    public string? PropertyId { get; set; }
    public CaretakerStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MonthlyCompensation { get; set; }
    public required string Responsibilities { get; set; }
    public string? Bio { get; set; }
    public string? ServiceArea { get; set; }
    /// <summary>Mean of service-request review ratings; null until the first review.</summary>
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
