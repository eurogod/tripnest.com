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

    /// <summary>Payment provider reference, recorded when funds are confirmed held.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>When funds were confirmed and moved into escrow. The auto-release grace period is measured from this moment.</summary>
    public DateTime? HeldAt { get; set; }

    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }

    /// <summary>Optimistic-concurrency token, mapped to Postgres' xmin system column.</summary>
    public uint Version { get; set; }
}
