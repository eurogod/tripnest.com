using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Escrow;

public class EscrowResponse
{
    public required string EscrowId { get; set; }
    public required string BookingId { get; set; }
    public decimal Amount { get; set; }
    public EscrowStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
}
