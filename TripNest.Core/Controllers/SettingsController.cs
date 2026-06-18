using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthService _authService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(
        IUserRepository userRepository,
        IAuthService authService,
        ILogger<SettingsController> logger)
    {
        _userRepository = userRepository;
        _authService = authService;
        _logger = logger;
    }

    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<object>.NotFound("User"));

            await _authService.ChangePasswordAsync(userId, request);
            return Ok(ApiResponse<object>.Ok("Password updated successfully", new { }));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Error updating password");
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpDelete("account")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteAccount()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<object>.NotFound("User"));

            user.IsActive = false;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok("Account deactivated successfully", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating account");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
