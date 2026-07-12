using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IUserRepository userRepository,
        IAuthService authService,
        INotificationService notificationService,
        ILogger<SettingsController> logger)
    {
        _userRepository = userRepository;
        _authService = authService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>Notification preferences (pass-through to the Communications module).</summary>
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>>> GetNotificationSettings()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var prefs = await _notificationService.GetPreferenceAsync(userId);
        return Ok(ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>.Ok("Notification settings retrieved", prefs));
    }

    [HttpPut("notifications")]
    [ProducesResponseType(typeof(ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>>> UpdateNotificationSettings([FromBody] DTOs.Notifications.UpdateCommunicationPreferenceRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var prefs = await _notificationService.UpdatePreferenceAsync(userId, request.SmsEnabled, request.EmailEnabled);
        return Ok(ApiResponse<DTOs.Notifications.CommunicationPreferenceResponse>.Ok(
            "Notification settings updated. Emergency safety alerts will still be sent regardless.", prefs));
    }

    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        // Failures flow to ExceptionHandlingMiddleware (InvalidOperationException → 400).
        await _authService.ChangePasswordAsync(userId, request);
        return Ok(ApiResponse<object>.Ok("Password updated successfully", new { }));
    }

    [HttpDelete("account")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAccount()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        user.IsActive = false;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok("Account deactivated successfully", new { }));
    }
}
