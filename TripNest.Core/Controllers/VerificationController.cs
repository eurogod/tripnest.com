using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Verification;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(IVerificationService verificationService, ILogger<VerificationController> logger)
    {
        _verificationService = verificationService;
        _logger = logger;
    }

    [HttpPost("start")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<VerificationStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<VerificationStatusResponse>>> StartVerification([FromBody] StartVerificationRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var response = await _verificationService.StartVerificationAsync(userId, request);

            return Ok(ApiResponse<VerificationStatusResponse>.Ok("Verification started successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Verification failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during verification");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<VerificationStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<VerificationStatusResponse>>> GetVerificationStatus()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var response = await _verificationService.GetVerificationStatusAsync(userId);

            return Ok(ApiResponse<VerificationStatusResponse>.Ok("Verification status retrieved", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve verification status: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving verification status");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
