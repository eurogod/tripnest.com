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
}
