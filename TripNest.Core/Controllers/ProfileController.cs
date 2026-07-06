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
public class ProfileController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IPhoneNumberValidator _phoneValidator;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserRepository userRepository, IFileStorage fileStorage, IPhoneNumberValidator phoneValidator, ILogger<ProfileController> logger)
    {
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _phoneValidator = phoneValidator;
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

        var pdf = Pdf.IdCardPdf.Render(user, await TryReadPhotoAsync(user.ProfilePhotoPath));
        return File(pdf, "application/pdf", $"tripnest-id-{user.TripNestId}.pdf");
    }

    // Best-effort load of the profile photo for embedding; placeholder initials are used if missing.
    // Reads through IFileStorage so the same stored path works under both local disk and Azure Blob,
    // and so the read is constrained to the uploads area (no arbitrary-path reads).
    private async Task<byte[]?> TryReadPhotoAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(path);
            if (stream is null)
                return null;

            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        }
        catch (Exceptions.ValidationException)
        {
            // Path outside the uploads area — treat as no photo rather than surfacing an error.
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load profile photo for ID card");
            return null;
        }
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetProfile()
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
            user.Username,
            user.Bio
        };

        return Ok(ApiResponse<object>.Ok("Profile retrieved", profile));
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound(ApiResponse<object>.NotFound("User"));

        user.FullName = request.FullName ?? user.FullName;
        user.Bio = request.Bio ?? user.Bio;
        if (request.PreferredLanguage is not null)
            user.PreferredLanguage = request.PreferredLanguage.Value;
        if (request.Username is not null)
        {
            var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();

            // A handle identifies one account: refuse any username another user already holds
            // (case-insensitively, so "Kwame" can't impersonate "kwame"). The unique index is
            // the backstop; this check gives a clean 400 instead of a database error.
            if (username is not null)
            {
                var lowered = username.ToLowerInvariant();
                var taken = (await _userRepository.FindAsync(u =>
                        u.Id != userId && u.Username != null && u.Username.ToLower() == lowered))
                    .Any();
                if (taken)
                    throw new InvalidOperationException("That username is already taken");
            }

            user.Username = username;
        }

        // Normalise the phone to E.164 (same as registration) so a profile edit can't store an
        // invalid number that later breaks SMS/OTP delivery.
        if (request.Phone is not null)
        {
            user.Phone = _phoneValidator.Normalize(request.Phone)
                ?? throw new InvalidOperationException("Please provide a valid phone number");
        }

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return Ok(ApiResponse<object>.Ok("Profile updated", new { }));
    }

    [HttpPost("photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UploadProfilePhoto(IFormFile photo)
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
}
