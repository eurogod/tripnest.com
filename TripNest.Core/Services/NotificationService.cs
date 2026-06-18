using TripNest.Core.DTOs.Notifications;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _logger = logger;
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
