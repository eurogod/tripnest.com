using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// The disbursement of a released escrow to the host, executed via Paystack Transfers. Exactly one
/// per escrow (unique index): created when the escrow is released, then driven to Paid/Failed by
/// the transfer webhook. The payout id doubles as the provider transfer reference, which makes the
/// provider call idempotent across retries.
/// </summary>
public class Payout
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string EscrowId { get; set; }
    public Escrow? Escrow { get; set; }

    public required string BookingId { get; set; }

    /// <summary>The host (property owner) receiving the money.</summary>
    public required string LandlordId { get; set; }

    /// <summary>What the guest paid (the escrow amount).</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>The platform's management fee withheld from the gross.</summary>
    public decimal FeeAmount { get; set; }

    /// <summary>Net amount actually transferred to the host.</summary>
    public decimal Amount { get; set; }

    public PayoutStatus Status { get; set; } = PayoutStatus.Pending;

    /// <summary>Paystack transfer code (TRF_...), set once a transfer has been initiated.</summary>
    public string? TransferCode { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the provider confirmed the transfer succeeded.</summary>
    public DateTime? PaidAt { get; set; }
}
