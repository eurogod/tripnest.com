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
[Route("api/auth/email")]
[Authorize]
[Produces("application/json")]
public class EmailVerificationController : ControllerBase
{
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly ILogger<EmailVerificationController> _logger;

    public EmailVerificationController(IEmailVerificationService emailVerificationService, ILogger<EmailVerificationController> logger)
    {
        _emailVerificationService = emailVerificationService;
        _logger = logger;
    }

    /// <summary>Sends a one-time code to the authenticated user's email address.</summary>
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<object>>> SendOtp()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _emailVerificationService.SendOtpAsync(userId);
        return Ok(ApiResponse<object>.Ok("Verification code sent", new { }));
    }

    /// <summary>Confirms ownership by checking the code; marks the email verified on success.</summary>
    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var ok = await _emailVerificationService.VerifyOtpAsync(userId, request.Code);
        return ok
            ? Ok(ApiResponse<object>.Ok("Email address verified", new { }))
            : BadRequest(ApiResponse<object>.BadRequest("Incorrect code"));
    }
}
