using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Hosted background service that drains <see cref="INotificationDispatchQueue"/> and performs the
/// SMS/email delivery for non-emergency notifications off the HTTP request path. A fresh DI scope is
/// created per job so the scoped senders / repositories / DbContext behave exactly as in a request.
/// After sending it updates the persisted notification's delivery flags. Sender failures are logged,
/// never thrown — a failed external send must not break the (already committed) in-app notification.
/// </summary>
public class NotificationDispatchService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INotificationDispatchQueue _queue;
    private readonly ILogger<NotificationDispatchService> _logger;

    public NotificationDispatchService(
        IServiceProvider serviceProvider,
        INotificationDispatchQueue queue,
        ILogger<NotificationDispatchService> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>How long after a stamped attempt a still-pending row is considered dead and
    /// eligible for the sweep. Long enough that an in-flight or just-finished attempt (including
    /// one interrupted by a restart right after the send) is not immediately duplicated.</summary>
    private static readonly TimeSpan StaleAttemptWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Two independent loops: the drain performs deliveries; the sweep recovers persisted
        // dispatch intent the in-memory queue lost (restart) or a delivery attempt left pending
        // (provider unreachable). The first sweep runs immediately as startup recovery.
        await Task.WhenAll(SweepLoopAsync(stoppingToken), DrainLoopAsync(stoppingToken));
    }

    private async Task DrainLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            NotificationDispatchJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessOneAsync(job);
        }
    }

    private async Task SweepLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RequeuePendingAsync(stoppingToken);
            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// The channel is in-memory, so jobs queued but not yet sent die with the process. The dispatch
    /// intent, however, is persisted on the notification row — requeue rows still pending whose
    /// last attempt (if any) is stale, so restarts and provider outages delay deliveries instead
    /// of dropping them (same recovery pattern as the verification queue).
    /// </summary>
    private async Task RequeuePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var staleBefore = DateTime.UtcNow - StaleAttemptWindow;
            var pending = (await notifications.FindAsync(
                n => (n.PendingSmsDispatch || n.PendingEmailDispatch) &&
                     (n.DispatchAttemptedAt == null || n.DispatchAttemptedAt < staleBefore))).ToList();
            if (pending.Count == 0)
                return;

            // One batched lookup for the recipients instead of a query per row.
            var userIds = pending.Select(n => n.UserId).Distinct().ToList();
            var usersById = (await users.FindAsync(u => userIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            foreach (var n in pending)
            {
                if (!usersById.TryGetValue(n.UserId, out var user))
                    continue;
                _queue.Enqueue(new NotificationDispatchJob(
                    n.Id, user.Phone, user.Email, n.Title, n.Message,
                    n.PendingSmsDispatch, n.PendingEmailDispatch));
            }

            _logger.LogInformation("Requeued {Count} undispatched notifications", pending.Count);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to requeue pending notification dispatches");
        }
    }

    private async Task ProcessOneAsync(NotificationDispatchJob job)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var sms = scope.ServiceProvider.GetRequiredService<ISmsSender>();
            var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

            var notification = await notifications.GetByIdAsync(job.NotificationId);
            if (notification is null)
                return;

            // Claim the attempt before touching the providers, so the sweep's stale window can
            // tell "attempt in flight / just happened" apart from "attempt died with the process".
            notification.DispatchAttemptedAt = DateTime.UtcNow;
            await notifications.UpdateAsync(notification);
            await notifications.SaveChangesAsync();

            var sentViaSms = false;
            var sentViaEmail = false;
            // "Answered" means the provider was reached and gave a verdict (success or refusal) —
            // those clear the pending flag; hammering a provider that said no won't help. A thrown
            // send means the provider was unreachable: keep the flag so the sweep retries later.
            var smsAnswered = true;
            var emailAnswered = true;

            if (job.SendSms && !string.IsNullOrWhiteSpace(job.Phone))
            {
                try { sentViaSms = await sms.SendSmsAsync(job.Phone, $"{job.Title}: {job.Body}"); }
                catch (Exception ex)
                {
                    smsAnswered = false;
                    _logger.LogError(ex, "SMS dispatch failed for notification {NotificationId}", job.NotificationId);
                }
            }

            if (job.SendEmail && !string.IsNullOrWhiteSpace(job.Email))
            {
                try { sentViaEmail = await email.SendAsync(job.Email, job.Title, $"<p>{job.Body}</p>"); }
                catch (Exception ex)
                {
                    emailAnswered = false;
                    _logger.LogError(ex, "Email dispatch failed for notification {NotificationId}", job.NotificationId);
                }
            }

            notification.SentViaSms = notification.SentViaSms || sentViaSms;
            notification.SentViaEmail = notification.SentViaEmail || sentViaEmail;
            if (smsAnswered)
                notification.PendingSmsDispatch = false;
            if (emailAnswered)
                notification.PendingEmailDispatch = false;
            await notifications.UpdateAsync(notification);
            await notifications.SaveChangesAsync();

            _logger.LogInformation(
                "Dispatched notification {NotificationId} — sms:{Sms} email:{Email}",
                job.NotificationId, sentViaSms, sentViaEmail);
        }
        catch (Exception ex)
        {
            // Guards the loop itself; an individual job failure must not kill the dispatcher.
            _logger.LogError(ex, "Unhandled error dispatching notification {NotificationId}", job.NotificationId);
        }
    }
}
