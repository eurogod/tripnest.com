using TripNest.Core.DTOs.Notifications;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRepository<CommunicationPreference> _preferenceRepository;
    private readonly ISmsSender _smsSender;
    private readonly IEmailSender _emailSender;
    private readonly INotificationDispatchQueue _dispatchQueue;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IRepository<CommunicationPreference> preferenceRepository,
        ISmsSender smsSender,
        IEmailSender emailSender,
        INotificationDispatchQueue dispatchQueue,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _preferenceRepository = preferenceRepository;
        _smsSender = smsSender;
        _emailSender = emailSender;
        _dispatchQueue = dispatchQueue;
        _logger = logger;
    }

    public async Task NotifyAsync(string userId, NotificationType type, string title, string body, bool isEmergency = false)
    {
        var preference = await GetOrCreatePreferenceAsync(userId);
        var user = await _userRepository.GetByIdAsync(userId);

        // Emergency alerts ignore the opt-out entirely; otherwise honour each channel's preference.
        var shouldSms = (isEmergency || preference.SmsEnabled) && user != null && !string.IsNullOrWhiteSpace(user.Phone);
        var shouldEmail = (isEmergency || preference.EmailEnabled) && user != null && !string.IsNullOrWhiteSpace(user.Email);

        if (isEmergency)
        {
            // Emergency alerts are sent INLINE so the caller (e.g. the SOS endpoint) gets a guaranteed
            // delivery attempt before the request returns — we never defer a safety alert to a queue.
            var emSms = shouldSms ? await SafeSendSmsAsync(user!.Phone, $"{title}: {body}") : false;
            var emEmail = shouldEmail ? await SafeSendEmailAsync(user!.Email, title, $"<p>{body}</p>") : false;

            await PersistNotificationAsync(userId, type, title, body, emSms, emEmail, isEmergency: true);

            _logger.LogInformation(
                "Notified user {UserId} ({Type}) inline (emergency) — sms:{Sms} email:{Email}",
                userId, type, emSms, emEmail);
            return;
        }

        // Non-emergency: persist the in-app record now (the source of truth, a fast DB write) and hand
        // the slow external SMS/email off to the background dispatcher so it never blocks the request.
        var notification = await PersistNotificationAsync(userId, type, title, body, sentViaSms: false, sentViaEmail: false, isEmergency: false);

        if (shouldSms || shouldEmail)
        {
            _dispatchQueue.Enqueue(new NotificationDispatchJob(
                notification.Id, user!.Phone, user.Email, title, body, shouldSms, shouldEmail));
        }

        _logger.LogInformation(
            "Notified user {UserId} ({Type}) — queued sms:{Sms} email:{Email}",
            userId, type, shouldSms, shouldEmail);
    }

    private async Task<Notification> PersistNotificationAsync(
        string userId, NotificationType type, string title, string body,
        bool sentViaSms, bool sentViaEmail, bool isEmergency)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = body,
            SentViaSms = sentViaSms,
            SentViaEmail = sentViaEmail,
            IsEmergencyOverride = isEmergency
        };

        await _notificationRepository.AddAsync(notification);
        await _notificationRepository.SaveChangesAsync();
        return notification;
    }

    public async Task<CommunicationPreferenceResponse> GetPreferenceAsync(string userId)
        => ToResponse(await GetOrCreatePreferenceAsync(userId));

    public async Task<CommunicationPreferenceResponse> UpdatePreferenceAsync(string userId, bool smsEnabled, bool emailEnabled)
    {
        var preference = await GetOrCreatePreferenceAsync(userId);
        preference.SmsEnabled = smsEnabled;
        preference.EmailEnabled = emailEnabled;
        preference.UpdatedAt = DateTime.UtcNow;
        await _preferenceRepository.UpdateAsync(preference);
        await _preferenceRepository.SaveChangesAsync();
        return ToResponse(preference);
    }

    private async Task<CommunicationPreference> GetOrCreatePreferenceAsync(string userId)
    {
        var existing = (await _preferenceRepository.FindAsync(p => p.UserId == userId))
            .FirstOrDefault();
        if (existing != null)
            return existing;

        var preference = new CommunicationPreference { UserId = userId };
        await _preferenceRepository.AddAsync(preference);
        await _preferenceRepository.SaveChangesAsync();
        return preference;
    }

    private async Task<bool> SafeSendSmsAsync(string phone, string message)
    {
        try { return await _smsSender.SendSmsAsync(phone, message); }
        catch (Exception ex) { _logger.LogError(ex, "SMS dispatch failed for {Phone}", phone); return false; }
    }

    private async Task<bool> SafeSendEmailAsync(string email, string subject, string html)
    {
        try { return await _emailSender.SendAsync(email, subject, html); }
        catch (Exception ex) { _logger.LogError(ex, "Email dispatch failed for {Email}", email); return false; }
    }

    private static CommunicationPreferenceResponse ToResponse(CommunicationPreference p) => new()
    {
        UserId = p.UserId,
        SmsEnabled = p.SmsEnabled,
        EmailEnabled = p.EmailEnabled
    };

    public async Task CreateAsync(string userId, string title, string message, string? relatedEntityId = null, string? relatedEntityType = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType
        };

        await _notificationRepository.AddAsync(notification);
        await _notificationRepository.SaveChangesAsync();

        _logger.LogInformation("Notification created for user {UserId}: {Title}", userId, title);
    }

    public async Task<PagedResult<NotificationResponse>> GetUserNotificationsAsync(string userId, int page, int pageSize)
    {
        try
        {
            var all = await _notificationRepository.GetByUserIdAsync(userId);
            var list = all.ToList();
            var totalCount = list.Count;
            var items = list
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(Map)
                .ToList();

            return new PagedResult<NotificationResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user {UserId}", userId);
            throw;
        }
    }

    public async Task MarkAsReadAsync(string notificationId, string userId)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null || notification.UserId != userId)
                throw new InvalidOperationException("Notification not found");

            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;

            await _notificationRepository.UpdateAsync(notification);
            await _notificationRepository.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} marked as read for user {UserId}", notificationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification {NotificationId} as read", notificationId);
            throw;
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        try
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId);
            var unread = notifications.Where(n => !n.IsRead).ToList();

            if (unread.Count == 0)
                return;

            var readAt = DateTime.UtcNow;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = readAt;
                await _notificationRepository.UpdateAsync(notification);
            }

            await _notificationRepository.SaveChangesAsync();

            _logger.LogInformation("Marked {Count} notifications as read for user {UserId}", unread.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read for user {UserId}", userId);
            throw;
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        try
        {
            var unread = await _notificationRepository.GetUnreadByUserIdAsync(userId);
            return unread.Count();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread notification count for user {UserId}", userId);
            throw;
        }
    }

    public async Task DeleteNotificationAsync(string notificationId, string userId)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(notificationId);
            if (notification == null)
                throw new InvalidOperationException("Notification not found");

            if (notification.UserId != userId)
                throw new UnauthorizedAccessException("You are not authorized to delete this notification");

            await _notificationRepository.DeleteAsync(notification);
            await _notificationRepository.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} deleted by user {UserId}", notificationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification {NotificationId}", notificationId);
            throw;
        }
    }

    private static NotificationResponse Map(Notification n) => new()
    {
        NotificationId = n.Id,
        UserId = n.UserId,
        Title = n.Title,
        Message = n.Message,
        IsRead = n.IsRead,
        RelatedEntityId = n.RelatedEntityId,
        RelatedEntityType = n.RelatedEntityType,
        CreatedAt = n.CreatedAt,
        ReadAt = n.ReadAt
    };
}
