using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserRepository userRepository, IFileStorage fileStorage, ILogger<ProfileController> logger)
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>Downloads the verified user's TripNest ID card as a PDF. Requires a verified identity.</summary>
    [HttpGet("id-card")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetIdCard()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        if (!user.IsVerified || string.IsNullOrWhiteSpace(user.TripNestId))
            return BadRequest(ApiResponse<object>.BadRequest(
                "Your identity isn't verified yet, so a TripNest ID card can't be issued."));

        var pdf = Pdf.IdCardPdf.Render(user, TryReadPhoto(user.ProfilePhotoPath));
        return File(pdf, "application/pdf", $"tripnest-id-{user.TripNestId}.pdf");
    }

    // Best-effort load of the profile photo for embedding; placeholder initials are used if missing.
    private static byte[]? TryReadPhoto(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var full = Path.IsPathRooted(path)
                ? path
                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", path.TrimStart('/'));
            return System.IO.File.Exists(full) ? System.IO.File.ReadAllBytes(full) : null;
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetProfile()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<object>.NotFound("User"));

            var profile = new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Phone,
                user.Role,
                user.IsVerified,
                user.EmailVerified,
                user.PhoneVerified,
                user.TripNestId,
                user.ProfilePhotoPath,
                user.Bio
            };

            return Ok(ApiResponse<object>.Ok("Profile retrieved", profile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] dynamic request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<object>.NotFound("User"));

            user.FullName = request.FullName ?? user.FullName;
            user.Phone = request.Phone ?? user.Phone;
            user.Bio = request.Bio ?? user.Bio;

            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok("Profile updated", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UploadProfilePhoto(IFormFile photo)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (photo == null || photo.Length == 0)
                return BadRequest(ApiResponse<object>.BadRequest("Photo file is required"));

            // Storage validates type + size and returns a servable path/URL (local disk or Azure Blob).
            var photoPath = await _fileStorage.SaveAsync("profiles", photo, UploadKind.Image);

            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.ProfilePhotoPath = photoPath;
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok("Profile photo uploaded", new { photoPath }));
        }
        catch (TripNest.Core.Exceptions.ValidationException ex)
        {
            // Rejected type/size from the storage validator.
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile photo");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
