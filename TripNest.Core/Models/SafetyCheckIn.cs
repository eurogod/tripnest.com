namespace TripNest.Core.Models;

public class SafetyCheckIn
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactEmail { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? AlertSentAt { get; set; }

    // Location is recorded only when the traveller consented to share it on this check-in.
    public bool LocationShared { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
