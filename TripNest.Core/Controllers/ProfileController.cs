using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Repositories;
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
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(IUserRepository userRepository, ILogger<ProfileController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
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

            var filename = $"profile-{userId}-{Guid.NewGuid()}{Path.GetExtension(photo.FileName)}";
            var filepath = Path.Combine("/uploads", "profiles", filename);

            using (var stream = new FileStream(filepath, FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.ProfilePhotoPath = filepath;
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }

            return Ok(ApiResponse<object>.Ok("Profile photo uploaded", new { photoPath = filepath }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading profile photo");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
