using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Caretaker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public CaretakerStatus Status { get; set; } = CaretakerStatus.Active;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public decimal? MonthlyCompensation { get; set; }
    public required string Responsibilities { get; set; }
}
