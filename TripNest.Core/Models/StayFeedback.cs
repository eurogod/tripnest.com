namespace TripNest.Core.Models;

public class StayFeedback
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string LandlordId { get; set; }
    public User? Landlord { get; set; }
    public required string TenantId { get; set; }
    public User? Tenant { get; set; }
    public int AccuracyRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int SafetyRating { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
