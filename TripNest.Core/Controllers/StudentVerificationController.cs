using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

/// <summary>
/// Student verification via an academic email (e.g. you@st.ug.edu.gh). Independent of Ghana Card
/// identity and contact-ownership OTPs. An active student status unlocks the student discount on
/// Student-stayType listings and expires after Student:ValidityDays (re-verify to renew).
/// </summary>
[ApiController]
[Route("api/auth/student")]
[Produces("application/json")]
[Authorize]
public class StudentVerificationController : ControllerBase
{
    public record SendStudentOtpRequest(string StudentEmail);
    public record VerifyStudentOtpRequest(string Code);

    private readonly IStudentVerificationService _studentService;

    public StudentVerificationController(IStudentVerificationService studentService) =>
        _studentService = studentService;

    /// <summary>The caller's student status (email, active flag, verified/expiry dates).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<StudentStatusResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<StudentStatusResponse>>> GetStatus()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var status = await _studentService.GetStatusAsync(userId);
        return Ok(ApiResponse<StudentStatusResponse>.Ok("Student status", status));
    }

    /// <summary>Emails a verification code to the given academic address (non-academic domains are rejected).</summary>
    [HttpPost("send-otp")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<object>>> SendOtp([FromBody] SendStudentOtpRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _studentService.SendOtpAsync(userId, request.StudentEmail);
        return Ok(ApiResponse<object>.Ok("Verification code sent to your student email", new { }));
    }

    /// <summary>Confirms the code — on success the caller is a verified student from today.</summary>
    [HttpPost("verify-otp")]
    [EnableRateLimiting("otp")]
    [ProducesResponseType(typeof(ApiResponse<StudentStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<StudentStatusResponse>>> VerifyOtp([FromBody] VerifyStudentOtpRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (!await _studentService.VerifyOtpAsync(userId, request.Code))
            return BadRequest(ApiResponse<object>.BadRequest("Incorrect code. Please try again."));

        var status = await _studentService.GetStatusAsync(userId);
        return Ok(ApiResponse<StudentStatusResponse>.Ok("Student status verified", status));
    }
}
