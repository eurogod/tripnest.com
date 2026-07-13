using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Maintenance;

public class MaintenanceResponse
{
    public required string MaintenanceId { get; set; }
    public required string PropertyId { get; set; }
    public required string ReportedByUserId { get; set; }
    public required string Description { get; set; }
    public MaintenanceStatus Status { get; set; }
    public string? TriageUrgency { get; set; }
    public string? TriageCategory { get; set; }
    public string? PhotoPath { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Resolution { get; set; }
}
