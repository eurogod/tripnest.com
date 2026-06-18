using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class ViewingRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string AgentId { get; set; }
    public Agent? Agent { get; set; }
    public required string TenantId { get; set; }
    public User? Tenant { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Notes { get; set; }
    public ViewingRequestStatus Status { get; set; } = ViewingRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
