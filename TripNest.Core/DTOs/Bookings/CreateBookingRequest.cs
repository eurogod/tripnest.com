using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.DTOs.Bookings;

public class CreateBookingRequest
{
    public required string PropertyId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
}
