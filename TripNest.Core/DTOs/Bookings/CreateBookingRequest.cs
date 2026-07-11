using TripNest.Core.DTOs.Properties;

namespace TripNest.Core.DTOs.Bookings;

public class CreateBookingRequest
{
    public required string PropertyId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }

    /// <summary>Number of guests for the stay. Defaults to 1 for clients that don't send it.</summary>
    [System.ComponentModel.DataAnnotations.Range(1, 16)]
    public int Guests { get; set; } = 1;

    /// <summary>
    /// Split billing: emails of registered co-travellers to share the cost with. The total is
    /// divided equally across everyone (including the booker, who absorbs rounding); each member
    /// pays their own share, and the booking confirms only when every share is paid within the
    /// split-payment window.
    /// </summary>
    public List<string>? SplitWithEmails { get; set; }
}
