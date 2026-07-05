using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Escrow;

public class EscrowResponse
{
    public required string EscrowId { get; set; }
    public required string BookingId { get; set; }
    public decimal Amount { get; set; }
    public EscrowStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public string? ReleaseReason { get; set; }
    public string? PaymentReference { get; set; }
    /// <summary>Hosted Paystack checkout URL to redirect the tenant to (set on initiate only).</summary>
    public string? CheckoutUrl { get; set; }

    /// <summary>
    /// True when the escrow is Released (cleared for payout) but the actual disbursement to the host
    /// has not been executed yet — real transfer via Paystack Transfers is not wired. Surfaces the
    /// distinction so host-facing UI shows "cleared for payout" rather than implying money has arrived.
    /// </summary>
    public bool DisbursementPending { get; set; }
}
