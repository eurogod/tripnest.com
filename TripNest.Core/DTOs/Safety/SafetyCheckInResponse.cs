namespace TripNest.Core.DTOs.Safety;

public class SafetyCheckInResponse
{
    public required string CheckInId { get; set; }
    public required string BookingId { get; set; }
    public bool IsCheckedIn { get; set; }
    public DateTime? CheckedInAt { get; set; }

    /// <summary>True if at least one channel (SMS/email) accepted the arrival message to the contact.</summary>
    public bool ContactNotified { get; set; }

    /// <summary>True if the traveller consented and location was attached to the check-in.</summary>
    public bool LocationShared { get; set; }
}
