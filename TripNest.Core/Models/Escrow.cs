using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Escrow
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    public decimal Amount { get; set; }
    public EscrowStatus Status { get; set; } = EscrowStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
