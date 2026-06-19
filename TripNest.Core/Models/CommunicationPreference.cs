namespace TripNest.Core.Models;

/// <summary>
/// Per-user opt-out for non-emergency SMS/email notifications. One row per user; both
/// channels default to enabled (opt-out, not opt-in). Emergency safety alerts ignore this.
/// </summary>
public class CommunicationPreference
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public bool SmsEnabled { get; set; } = true;
    public bool EmailEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
