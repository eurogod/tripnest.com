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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

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

    /// <summary>
    /// The channel is in-memory, so jobs queued but not yet sent die with the process. The dispatch
    /// intent, however, is persisted on the notification row — on startup, requeue those rows so a
    /// restart delays deliveries instead of dropping them (same recovery pattern as the
    /// verification queue).
    /// </summary>
    private async Task RequeuePendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var pending = (await notifications.FindAsync(
                n => n.PendingSmsDispatch || n.PendingEmailDispatch)).ToList();
            if (pending.Count == 0)
                return;

            foreach (var n in pending)
            {
                var user = await users.GetByIdAsync(n.UserId);
                if (user is null)
                    continue;
                _queue.Enqueue(new NotificationDispatchJob(
                    n.Id, user.Phone, user.Email, n.Title, n.Message,
                    n.PendingSmsDispatch, n.PendingEmailDispatch));
            }

            _logger.LogInformation("Requeued {Count} undispatched notifications from before the restart", pending.Count);
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

            var sentViaSms = false;
            var sentViaEmail = false;

            if (job.SendSms && !string.IsNullOrWhiteSpace(job.Phone))
            {
                try { sentViaSms = await sms.SendSmsAsync(job.Phone, $"{job.Title}: {job.Body}"); }
                catch (Exception ex) { _logger.LogError(ex, "SMS dispatch failed for notification {NotificationId}", job.NotificationId); }
            }

            if (job.SendEmail && !string.IsNullOrWhiteSpace(job.Email))
            {
                try { sentViaEmail = await email.SendAsync(job.Email, job.Title, $"<p>{job.Body}</p>"); }
                catch (Exception ex) { _logger.LogError(ex, "Email dispatch failed for notification {NotificationId}", job.NotificationId); }
            }

            // Record what actually went out, and clear the pending flags now that a delivery
            // attempt has been made — attempted-but-failed sends are NOT retried on restart
            // (the provider was reachable and said no; hammering it again won't help).
            var notification = await notifications.GetByIdAsync(job.NotificationId);
            if (notification != null)
            {
                notification.SentViaSms = sentViaSms;
                notification.SentViaEmail = sentViaEmail;
                notification.PendingSmsDispatch = false;
                notification.PendingEmailDispatch = false;
                await notifications.UpdateAsync(notification);
                await notifications.SaveChangesAsync();
            }

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
