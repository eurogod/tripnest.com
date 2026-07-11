using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// Immutable audit record of one escrow status transition — who moved the money-state, from what,
/// to what, and why. Written in the same save as the transition itself, so the trail can never
/// disagree with the escrow row. This is the forensic source for dispute investigations; log lines
/// have a retention window, these rows do not.
/// </summary>
public class EscrowEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string EscrowId { get; set; }
    public Escrow? Escrow { get; set; }
    public required string BookingId { get; set; }
    public EscrowStatus FromStatus { get; set; }
    public EscrowStatus ToStatus { get; set; }
    /// <summary>The user id that drove the transition, or a system principal
    /// ("system:auto-release", "payment-provider", "admin").</summary>
    public required string Actor { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
