using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var user = await _authService.RegisterAsync(request);

            return Created($"api/auth/{user.Id}", ApiResponse<object>.Created("User", new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role
            }));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Registration failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var response = await _authService.LoginAsync(request);

            return Ok(ApiResponse<LoginResponse>.Ok("Login successful", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Login failed for email: {Email}", request.Email);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var response = await _authService.RefreshTokenAsync(request.RefreshToken);

            return Ok(ApiResponse<LoginResponse>.Ok("Token refreshed successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Token refresh failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during token refresh");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<UserProfileDto>> GetCurrentUser()
    {
        try
        {
            var userId = User.GetUserId();
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var fullName = User.FindFirst(ClaimTypes.Name)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var userProfile = new UserProfileDto
            {
                UserId = userId,
                FullName = fullName ?? "Unknown",
                Email = email ?? "Unknown",
                Role = Enum.Parse<TripNest.Core.Enums.UserRole>(role ?? "Tenant")
            };

            return Ok(ApiResponse<UserProfileDto>.Ok("User profile retrieved", userProfile));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving user profile");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _authService.LogoutAsync(userId);
            return Ok(ApiResponse<object>.Ok("Logged out successfully", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during logout");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            await _authService.ChangePasswordAsync(userId, request);

            var response = new { message = "Password changed successfully" };
            return Ok(ApiResponse<object>.Ok("Password changed successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Password change failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error changing password");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            var (user, resetToken) = await _authService.ForgotPasswordAsync(request.Email);

            var response = new { message = "Password reset token sent to email" };
            return Ok(ApiResponse<object>.Ok("Password reset initiated", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Forgot password failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during forgot password");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            if (request.NewPassword != request.ConfirmPassword)
                return BadRequest(ApiResponse<object>.BadRequest("Passwords do not match"));

            await _authService.ResetPasswordAsync(request.Email, request.ResetToken, request.NewPassword);

            var response = new { message = "Password reset successfully" };
            return Ok(ApiResponse<object>.Ok("Password reset successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Password reset failed: {Message}", ex.Message);
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during password reset");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
