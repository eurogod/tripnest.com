using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Maintenance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string ReportedByUserId { get; set; }
    public User? ReportedByUser { get; set; }
    public required string Description { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Reported;
    public string? PhotoPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Resolution { get; set; }
}
