using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Bookings;

public class BookingShareResponse
{
    public required string ShareId { get; set; }
    public required string BookingId { get; set; }
    public required string ParticipantUserId { get; set; }
    public string? ParticipantName { get; set; }
    public decimal Amount { get; set; }
    public BookingShareStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Set only on the initiate-payment response — the participant's checkout link.</summary>
    public string? CheckoutUrl { get; set; }
}
