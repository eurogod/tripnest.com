using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Caretakers;

public class CaretakerResponse
{
    public required string CaretakerId { get; set; }
    public required string UserId { get; set; }
    public required string PropertyId { get; set; }
    public CaretakerStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? MonthlyCompensation { get; set; }
    public required string Responsibilities { get; set; }
}
