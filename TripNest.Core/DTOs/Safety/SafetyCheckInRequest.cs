namespace TripNest.Core.DTOs.Safety;

public class SafetyCheckInRequest
{
    public required string BookingId { get; set; }

    /// <summary>Optional per-request override; falls back to the user's saved trusted contact.</summary>
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>Explicit per-check-in consent to share location. The app must ask the user first;
    /// the server attaches/persists coordinates only when this is true and coordinates are present.</summary>
    public bool ShareLocation { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
