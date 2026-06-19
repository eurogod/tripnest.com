using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Notifications;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/communication-preferences")]
[Authorize]
[Produces("application/json")]
public class CommunicationPreferencesController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public CommunicationPreferencesController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Gets the current user's SMS/email notification preferences.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<CommunicationPreferenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CommunicationPreferenceResponse>>> GetMine()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var prefs = await _notificationService.GetPreferenceAsync(userId);
        return Ok(ApiResponse<CommunicationPreferenceResponse>.Ok("Preferences retrieved", prefs));
    }

    /// <summary>
    /// Updates SMS/email opt-out. Note: emergency safety alerts are always sent regardless of
    /// this setting — surfaced in the response message so the UI can warn the user.
    /// </summary>
    [HttpPut("mine")]
    [ProducesResponseType(typeof(ApiResponse<CommunicationPreferenceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CommunicationPreferenceResponse>>> UpdateMine([FromBody] UpdateCommunicationPreferenceRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var prefs = await _notificationService.UpdatePreferenceAsync(userId, request.SmsEnabled, request.EmailEnabled);
        return Ok(ApiResponse<CommunicationPreferenceResponse>.Ok(
            "Preferences updated. Note: emergency safety alerts will still be sent by SMS and email regardless of this setting.",
            prefs));
    }
}
