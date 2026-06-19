using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public NotificationType Type { get; set; } = NotificationType.General;
    public required string Title { get; set; }
    public required string Message { get; set; }
    public bool IsRead { get; set; } = false;

    // Which channels this notification was actually dispatched on (in addition to in-app).
    public bool SentViaSms { get; set; }
    public bool SentViaEmail { get; set; }
    // True only when a SafetyAlert bypassed the user's opt-out — auditable.
    public bool IsEmergencyOverride { get; set; }

    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
