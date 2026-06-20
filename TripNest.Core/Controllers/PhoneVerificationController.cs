using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Exceptions;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/auth/phone")]
[Authorize]
[Produces("application/json")]
public class PhoneVerificationController : ControllerBase
{
    private readonly IPhoneVerificationService _phoneVerificationService;
    private readonly ILogger<PhoneVerificationController> _logger;

    public PhoneVerificationController(IPhoneVerificationService phoneVerificationService, ILogger<PhoneVerificationController> logger)
    {
        _phoneVerificationService = phoneVerificationService;
        _logger = logger;
    }

    /// <summary>Sends a one-time code to the authenticated user's phone (SMS or WhatsApp).</summary>
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<object>>> SendOtp([FromBody] SendOtpRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        try
        {
            await _phoneVerificationService.SendOtpAsync(userId, request.Channel);
            return Ok(ApiResponse<object>.Ok("Verification code sent", new { }));
        }
        catch (TooManyRequestsException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, ApiResponse<object>.TooManyRequests(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending phone OTP");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>Confirms ownership by checking the code; marks the phone verified on success.</summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        try
        {
            var ok = await _phoneVerificationService.VerifyOtpAsync(userId, request.Code);
            return ok
                ? Ok(ApiResponse<object>.Ok("Phone number verified", new { }))
                : BadRequest(ApiResponse<object>.BadRequest("Incorrect code"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying phone OTP");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
