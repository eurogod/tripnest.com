using TripNest.Core.Enums;

using System.ComponentModel.DataAnnotations;

namespace TripNest.Core.DTOs.Payouts;

/// <summary>Registers (or replaces) where the host's payouts go.</summary>
public class UpsertPayoutAccountRequest
{
    /// <summary>"mobile_money" (MoMo wallet — codes MTN, ATL, VOD) or "ghipss" (bank account).</summary>
    [StringLength(20)]
    public required string Channel { get; set; }

    /// <summary>Paystack bank/provider code, e.g. "MTN".</summary>
    [StringLength(20)]
    public required string ProviderCode { get; set; }

    /// <summary>MoMo wallet number or bank account number.</summary>
    [StringLength(30, MinimumLength = 8)]
    public required string AccountNumber { get; set; }

    /// <summary>Account holder name as registered with the provider.</summary>
    [StringLength(100, MinimumLength = 2)]
    public required string AccountName { get; set; }
}

public class PayoutAccountResponse
{
    public required string Channel { get; set; }
    public required string ProviderCode { get; set; }
    /// <summary>Masked — only the last 3 digits are exposed.</summary>
    public required string AccountNumber { get; set; }
    [StringLength(100, MinimumLength = 2)]
    public required string AccountName { get; set; }
    /// <summary>True once the account is registered with the payment provider and can receive transfers.</summary>
    public bool ProviderRegistered { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PayoutResponse
{
    public required string PayoutId { get; set; }
    /// <summary>Null for payouts sourced from a rent invoice or damage claim instead of an escrow.</summary>
    public string? EscrowId { get; set; }
    public required string BookingId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal Amount { get; set; }
    public PayoutStatus Status { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}
