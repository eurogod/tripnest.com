using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Bookings;

public class BookingResponse
{
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Guests { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
