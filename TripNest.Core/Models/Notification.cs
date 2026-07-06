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

    // Dispatch intent persisted with the row, so queued-but-unsent SMS/email survive a process
    // restart: the dispatcher's sweep requeues rows where these are still true and clears them
    // once the provider answers a delivery attempt (success or refusal). A send that THROWS
    // (provider unreachable) keeps the flag set so the sweep retries it later.
    public bool PendingSmsDispatch { get; set; }
    public bool PendingEmailDispatch { get; set; }
    // Stamped just before each delivery attempt. The sweep only requeues rows whose last attempt
    // is stale, so a restart moments after a successful send doesn't immediately resend it.
    public DateTime? DispatchAttemptedAt { get; set; }
    // True only when a SafetyAlert bypassed the user's opt-out — auditable.
    public bool IsEmergencyOverride { get; set; }

    public string? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
