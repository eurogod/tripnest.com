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
    /// <summary>Best-effort AI triage at report time (whitelisted labels; null when AI is off/failed).</summary>
    public string? TriageUrgency { get; set; }
    public string? TriageCategory { get; set; }
    public string? PhotoPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? Resolution { get; set; }
}
