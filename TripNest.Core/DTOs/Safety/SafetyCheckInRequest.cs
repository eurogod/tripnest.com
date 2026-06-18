namespace TripNest.Core.DTOs.Safety;

public class SafetyCheckInRequest
{
    public required string BookingId { get; set; }
    public string? EmergencyContactPhone { get; set; }
}
