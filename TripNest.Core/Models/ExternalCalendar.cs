namespace TripNest.Core.Models;

/// <summary>
/// An external iCal feed (Airbnb, VRBO, Booking.com, …) a host linked to a listing. A background
/// worker (and the manual sync endpoint) fetches it and mirrors its busy ranges into
/// <see cref="PropertyBlockedDate"/> rows tagged with this calendar's id, so stays booked on the
/// other platform block the dates here — the import half of cross-platform calendar sync.
/// </summary>
public class ExternalCalendar
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    /// <summary>Host-facing label, e.g. "Airbnb".</summary>
    public required string Name { get; set; }
    public required string FeedUrl { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    /// <summary>Last fetch/parse failure, cleared on a successful sync.</summary>
    public string? LastSyncError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
