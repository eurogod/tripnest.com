using TripNest.Core.DTOs.Payouts;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// Host disbursements via Paystack Transfers: payout-account registration, the payout created
/// whenever an escrow is released, and the transfer webhook that drives it to Paid/Failed.
/// </summary>
public interface IPayoutService
{
    /// <summary>The caller's payout account (masked), or null if none registered.</summary>
    Task<PayoutAccountResponse?> GetMyAccountAsync(string userId);

    /// <summary>Registers/replaces the caller's payout destination and registers it with the
    /// provider as a transfer recipient.</summary>
    Task<PayoutAccountResponse> UpsertMyAccountAsync(string userId, UpsertPayoutAccountRequest request);

    /// <summary>The caller's payouts, newest first.</summary>
    Task<PagedResult<PayoutResponse>> GetMyPayoutsAsync(string userId, int page, int pageSize);

    /// <summary>
    /// Creates the payout for a just-released escrow (idempotent — one payout per escrow) and
    /// attempts the provider transfer immediately when the host has a registered account.
    /// Never throws for provider/account problems: the payout stays Pending/Failed for retry,
    /// because a payout hiccup must not undo the escrow release that triggered it.
    /// <paramref name="grossOverride"/> pays out only part of the escrow — the host's retained
    /// share when a cancellation policy refunds the tenant the rest.
    /// </summary>
    Task CreateForReleasedEscrowAsync(Escrow escrow, string landlordId, decimal? grossOverride = null);

    /// <summary>
    /// Creates the payout for a just-paid monthly rent invoice (idempotent — one payout per
    /// invoice) and attempts the provider transfer immediately. Same never-throws contract as the
    /// escrow path: a payout hiccup must not unwind the rent payment that triggered it.
    /// </summary>
    Task CreateForPaidRentAsync(RentInvoice invoice);

    /// <summary>Re-attempts a Pending or Failed payout (e.g. after the host fixes their account).</summary>
    Task<PayoutResponse> RetryAsync(string payoutId, string userId);

    /// <summary>Applies a transfer.success / transfer.failed / transfer.reversed webhook event.
    /// The reference is the payout id.</summary>
    Task HandleTransferWebhookAsync(string eventType, string reference, string? failureReason);
}
