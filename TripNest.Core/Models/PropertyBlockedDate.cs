namespace TripNest.Core.Models;

public class PropertyBlockedDate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string BlockedByUserId { get; set; }
    public User? BlockedByUser { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Reason { get; set; }
    /// <summary>Set when this range was imported from an external iCal feed; a re-sync of that
    /// feed replaces exactly its own rows and never touches manual blocks (null).</summary>
    public string? ExternalCalendarId { get; set; }
    public ExternalCalendar? ExternalCalendar { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
