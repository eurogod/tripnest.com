using System.Collections.Concurrent;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Tests;

/// <summary>Test double that records SMS sends instead of calling TextBee.</summary>
public class RecordingSmsSender : ISmsSender
{
    public ConcurrentBag<(string Phone, string Message)> Sent { get; } = new();

    public Task<bool> SendSmsAsync(string phoneNumber, string message)
    {
        Sent.Add((phoneNumber, message));
        return Task.FromResult(true);
    }
}

/// <summary>Test double that records emails instead of sending over SMTP.</summary>
public class RecordingEmailSender : IEmailSender
{
    public ConcurrentBag<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
    {
        Sent.Add((toEmail, subject, htmlBody));
        return Task.FromResult(true);
    }
}

/// <summary>Test double for the payment gateway with configurable verify behaviour.</summary>
public class StubPaymentGateway : IPaymentGateway
{
    public bool VerifySucceeds { get; set; } = true;
    public decimal VerifyAmount { get; set; }

    /// <summary>Every refund issued through the gateway, so tests can assert money actually moved.</summary>
    public ConcurrentBag<(string Reference, decimal Amount)> Refunds { get; } = new();

    public Task<PaymentInitResult> InitiatePaymentAsync(decimal amount, string currency, string customerEmail, string bookingId, string? callbackUrl = null)
        => Task.FromResult(new PaymentInitResult(true, "https://stub.checkout/pay", $"STUB-{bookingId}"));

    public Task<PaymentVerifyResult> VerifyPaymentAsync(string reference)
        => Task.FromResult(new PaymentVerifyResult(VerifySucceeds, VerifyAmount));

    public Task<bool> RefundAsync(string reference, decimal amount)
    {
        Refunds.Add((reference, amount));
        return Task.FromResult(true);
    }

    /// <summary>Every transfer initiated through the gateway, so tests can assert payouts moved money.</summary>
    public ConcurrentBag<(string RecipientCode, decimal Amount, string Reference)> Transfers { get; } = new();

    /// <summary>When false, recipient registration fails like a provider rejection.</summary>
    public bool RecipientSucceeds { get; set; } = true;

    /// <summary>When false, transfers fail like a provider rejection.</summary>
    public bool TransferSucceeds { get; set; } = true;

    /// <summary>Status reported for successful transfers ("success" = settled immediately,
    /// "pending" = webhook confirms later).</summary>
    public string TransferStatus { get; set; } = "success";

    public Task<TransferRecipientResult> CreateTransferRecipientAsync(
        string accountName, string accountNumber, string providerCode, string channel, string currency)
        => Task.FromResult(RecipientSucceeds
            ? new TransferRecipientResult(true, $"RCP_STUB_{accountNumber[^3..]}", null)
            : new TransferRecipientResult(false, null, "Stub rejected the recipient"));

    public Task<TransferResult> InitiateTransferAsync(
        decimal amount, string currency, string recipientCode, string reference, string reason)
    {
        if (!TransferSucceeds)
            return Task.FromResult(new TransferResult(false, null, null, "Stub rejected the transfer"));

        Transfers.Add((recipientCode, amount, reference));
        return Task.FromResult(new TransferResult(true, $"TRF_STUB_{reference[..Math.Min(8, reference.Length)]}", TransferStatus, null));
    }
}
