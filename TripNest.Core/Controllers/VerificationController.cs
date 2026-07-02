using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Verification;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class VerificationController : ControllerBase
{
    private readonly IVerificationService _verificationService;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<VerificationController> _logger;

    public VerificationController(IVerificationService verificationService, IFileStorage fileStorage, ILogger<VerificationController> logger)
    {
        _verificationService = verificationService;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>
    /// Uploads the verification selfie and returns the stored path to pass to <c>start</c>. Having the
    /// server own the upload (rather than the client sending a filesystem path) is what makes the
    /// selfie reference safe — the stored path is validated on read and can't point outside uploads.
    /// </summary>
    [HttpPost("selfie")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> UploadSelfie(IFormFile selfie)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (selfie == null || selfie.Length == 0)
                return BadRequest(ApiResponse<object>.BadRequest("Selfie file is required"));

            var path = await _fileStorage.SaveAsync($"verifications/{userId}", selfie, UploadKind.Image);
            return Ok(ApiResponse<object>.Ok("Selfie uploaded", new { selfiePhotoPath = path }));
        }
        catch (TripNest.Core.Exceptions.ValidationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading verification selfie");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
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
            var userId = User.GetUserId();

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
            var userId = User.GetUserId();

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
