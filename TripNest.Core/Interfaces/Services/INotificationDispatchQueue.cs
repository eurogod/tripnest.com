namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// A unit of out-of-band notification delivery. Carries everything the dispatcher needs to send,
/// plus the id of the already-persisted in-app <c>Notification</c> so its delivery flags
/// (<c>SentViaSms</c>/<c>SentViaEmail</c>) can be updated once the external send completes.
/// </summary>
public record NotificationDispatchJob(
    string NotificationId,
    string? Phone,
    string? Email,
    string Title,
    string Body,
    bool SendSms,
    bool SendEmail);

/// <summary>
/// In-process queue that hands non-emergency SMS/email delivery off the HTTP request thread.
/// Registered as a singleton so the request path (enqueue) and the hosted dispatcher (dequeue)
/// share one instance. Emergency alerts are NOT queued — they are sent inline so the caller gets a
/// guaranteed delivery attempt before the request returns.
/// </summary>
public interface INotificationDispatchQueue
{
    /// <summary>Queues a delivery job. Never blocks (the backing channel is unbounded).</summary>
    void Enqueue(NotificationDispatchJob job);

    /// <summary>Awaits the next job for the hosted dispatcher.</summary>
    ValueTask<NotificationDispatchJob> DequeueAsync(CancellationToken cancellationToken);
}
