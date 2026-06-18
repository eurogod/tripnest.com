using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Notifications;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(INotificationService notificationService, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's notifications
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationResponse>>>> GetMyNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<PagedResult<NotificationResponse>>.UnAuthorized());

            var notifications = await _notificationService.GetUserNotificationsAsync(userId, page, pageSize);
            return Ok(ApiResponse<PagedResult<NotificationResponse>>.Ok("Notifications retrieved", notifications));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications");
            return StatusCode(500, ApiResponse<PagedResult<NotificationResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    [HttpPatch("{id}/read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> MarkAsRead(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<NotificationResponse>.UnAuthorized());

            await _notificationService.MarkAsReadAsync(id, userId);
            return Ok(ApiResponse<NotificationResponse>.Ok("Notification marked as read"));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ApiResponse<NotificationResponse>.NotFound("Notification"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
            return StatusCode(500, ApiResponse<NotificationResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    [HttpPatch("mark-all-read")]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> MarkAllAsRead()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<NotificationResponse>.UnAuthorized());

            await _notificationService.MarkAllAsReadAsync(userId);
            return Ok(ApiResponse<NotificationResponse>.Ok("All notifications marked as read", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notifications as read");
            return StatusCode(500, ApiResponse<NotificationResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Ok(ApiResponse<object>.Ok("Unread count retrieved", new { unreadCount = count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving unread count");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Delete notification
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<NotificationResponse>>> DeleteNotification(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<NotificationResponse>.UnAuthorized());

            await _notificationService.DeleteNotificationAsync(id, userId);
            return Ok(ApiResponse<NotificationResponse>.Ok("Notification deleted", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting notification");
            return StatusCode(500, ApiResponse<NotificationResponse>.InternalServerError());
        }
    }
}
