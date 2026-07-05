using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.DTOs.Bookings;

public class CreateBookingRequest
{
    public required string PropertyId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    /// <summary>Number of guests for the stay. Defaults to 1 for clients that don't send it.</summary>
    public int Guests { get; set; } = 1;
}
