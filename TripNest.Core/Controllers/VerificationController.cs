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

    public VerificationController(IVerificationService verificationService, IFileStorage fileStorage)
    {
        _verificationService = verificationService;
        _fileStorage = fileStorage;
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
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (selfie == null || selfie.Length == 0)
            return BadRequest(ApiResponse<object>.BadRequest("Selfie file is required"));

        var path = await _fileStorage.SaveAsync($"verifications/{userId}", selfie, UploadKind.Image);
        return Ok(ApiResponse<object>.Ok("Selfie uploaded", new { selfiePhotoPath = path }));
    }

    [HttpPost("start")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<VerificationStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ApiResponse<VerificationStatusResponse>>> StartVerification([FromBody] StartVerificationRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var response = await _verificationService.StartVerificationAsync(userId, request);
        return Ok(ApiResponse<VerificationStatusResponse>.Ok("Verification started successfully", response));
    }

    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<VerificationStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<VerificationStatusResponse>>> GetVerificationStatus()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var response = await _verificationService.GetVerificationStatusAsync(userId);
        return Ok(ApiResponse<VerificationStatusResponse>.Ok("Verification status retrieved", response));
    }
}
