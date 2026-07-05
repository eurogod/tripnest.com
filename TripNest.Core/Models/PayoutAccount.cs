namespace TripNest.Core.Models;

/// <summary>
/// Where a host's payouts go: a Ghana mobile-money wallet or bank account, registered with
/// Paystack as a transfer recipient. One per user (unique index). The full account number is
/// needed to (re)create the provider recipient; API responses only ever expose a masked form.
/// </summary>
public class PayoutAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string UserId { get; set; }
    public User? User { get; set; }

    /// <summary>"mobile_money" (MoMo wallet) or "ghipss" (bank account).</summary>
    public required string Channel { get; set; }

    /// <summary>Paystack bank/provider code — e.g. MTN, ATL, VOD for mobile money.</summary>
    public required string ProviderCode { get; set; }

    /// <summary>MoMo wallet number or bank account number.</summary>
    public required string AccountNumber { get; set; }

    /// <summary>Account holder name as registered with the provider.</summary>
    public required string AccountName { get; set; }

    /// <summary>Paystack transfer recipient code (RCP_...), set when registered with the provider.</summary>
    public string? RecipientCode { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
