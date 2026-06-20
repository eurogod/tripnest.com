namespace TripNest.Core.Interfaces.Services;

public record PaymentInitResult(bool Success, string CheckoutUrl, string Reference);
public record PaymentVerifyResult(bool Success, decimal Amount);

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
}
