namespace TripNest.Core.Models;

public class SafetyCheckIn
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? AlertSentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
