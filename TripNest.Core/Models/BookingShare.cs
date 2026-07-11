using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// One member's slice of a group (split-billing) booking. Shares are created with the booking;
/// each participant pays their own share through their own checkout, and the booking's escrow is
/// held — and the booking confirmed — only when every share is paid. If the group doesn't finish
/// paying within the split-payment window, the booking is cancelled and paid shares are refunded.
/// </summary>
public class BookingShare
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public required string ParticipantUserId { get; set; }
    public User? Participant { get; set; }
    public decimal Amount { get; set; }
    public BookingShareStatus Status { get; set; } = BookingShareStatus.Pending;
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
