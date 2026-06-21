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
