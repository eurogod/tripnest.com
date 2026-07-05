namespace TripNest.Core.Interfaces.Services;

public record PaymentInitResult(bool Success, string CheckoutUrl, string Reference);
/// <summary>Simulated=true means the gateway is unconfigured and couldn't know the real amount —
/// callers should fall back to the amount they expect instead of treating 0 as an underpayment.</summary>
public record PaymentVerifyResult(bool Success, decimal Amount, bool Simulated = false);
public record TransferRecipientResult(bool Success, string? RecipientCode, string? Error);
public record TransferResult(bool Success, string? TransferCode, string? Status, string? Error);

/// <summary>
/// Abstraction over a payment provider (Paystack). Implementations must degrade gracefully
/// when no API key is configured so local/dev flows still work without real credentials.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Starts a checkout; returns the hosted checkout URL + transaction reference.</summary>
    Task<PaymentInitResult> InitiatePaymentAsync(decimal amount, string currency, string customerEmail, string bookingId, string? callbackUrl = null);

    /// <summary>Verifies a transaction reference server-side.</summary>
    Task<PaymentVerifyResult> VerifyPaymentAsync(string reference);

    /// <summary>Refunds (all or part of) a transaction.</summary>
    Task<bool> RefundAsync(string reference, decimal amount);

    /// <summary>Registers a payout destination (MoMo wallet or bank account) with the provider,
    /// returning the recipient code used for transfers.</summary>
    Task<TransferRecipientResult> CreateTransferRecipientAsync(
        string accountName, string accountNumber, string providerCode, string channel, string currency);

    /// <summary>Sends money from the platform balance to a registered recipient. The reference
    /// makes the call idempotent — the provider executes one transfer per reference.</summary>
    Task<TransferResult> InitiateTransferAsync(
        decimal amount, string currency, string recipientCode, string reference, string reason);
}
