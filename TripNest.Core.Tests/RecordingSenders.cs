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

/// <summary>Test double for the AI client with configurable availability and canned suggestions.</summary>
public class StubAiClient : IAiClient
{
    /// <summary>When false, behaves like a server with no Ai:ApiKey configured.</summary>
    public bool Configured { get; set; } = true;

    /// <summary>What the next generation call returns; null simulates a provider failure.</summary>
    public TripNest.Core.DTOs.Properties.ListingCopySuggestion? NextSuggestion { get; set; } = new()
    {
        Title = "Sunny 2-Bedroom Near Campus",
        Description = "A bright apartment close to the university with reliable water and parking.",
        Highlights = new List<string> { "5 min to campus", "Dedicated parking", "Backup water supply" },
    };

    /// <summary>Every generation request, so tests can assert what was sent to the model.</summary>
    public ConcurrentBag<(string PropertyId, int PhotoCount)> Requests { get; } = new();

    public bool IsConfigured => Configured;

    /// <summary>The language requested on the last generation call, for assertions.</summary>
    public TripNest.Core.Enums.Language? LastLanguage { get; private set; }

    public Task<TripNest.Core.DTOs.Properties.ListingCopySuggestion?> GenerateListingCopyAsync(
        TripNest.Core.Models.Property property, IReadOnlyList<AiImage> photos,
        TripNest.Core.Enums.Language language, CancellationToken cancellationToken = default)
    {
        Requests.Add((property.Id, photos.Count));
        LastLanguage = language;
        return Task.FromResult(Configured ? NextSuggestion : null);
    }

    /// <summary>What the next CompleteAsync returns; null simulates a provider failure.</summary>
    public string? NextCompletion { get; set; }

    /// <summary>Every completion request, so tests can assert what was sent to the model.</summary>
    public ConcurrentBag<(string SystemPrompt, string UserPrompt)> Completions { get; } = new();

    public Task<string?> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        Completions.Add((systemPrompt, userPrompt));
        return Task.FromResult(Configured ? NextCompletion : null);
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

/// <summary>Serves canned iCal documents keyed by feed URL — no network in tests.</summary>
public class StubIcalFeedFetcher : TripNest.Core.Interfaces.Services.IIcalFeedFetcher
{
    /// <summary>Feed URL → ICS body. A missing URL throws, simulating a dead feed.</summary>
    public Dictionary<string, string> Feeds { get; } = new();

    public Task<string> FetchAsync(string url, CancellationToken cancellationToken = default) =>
        Feeds.TryGetValue(url, out var ics)
            ? Task.FromResult(ics)
            : throw new HttpRequestException($"Stub has no feed for {url}");
}
