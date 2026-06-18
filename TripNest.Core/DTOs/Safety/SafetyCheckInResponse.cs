namespace TripNest.Core.DTOs.Safety;

public class SafetyCheckInResponse
{
    public required string CheckInId { get; set; }
    public required string BookingId { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }
}
