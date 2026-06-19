using System.Security.Cryptography;
using System.Text;
using TripNest.Core.DTOs.Auth;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Security;

namespace TripNest.Core.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUserRepository userRepository, ITokenService tokenService, ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<User> RegisterAsync(RegisterRequest request)
    {
        if (request.Role == UserRole.Admin)
            throw new InvalidOperationException("Admin accounts cannot be self-registered");

        if (request.Password != request.ConfirmPassword)
            throw new InvalidOperationException("Passwords do not match");

        PasswordPolicy.Validate(request.Password);

        if (await _userRepository.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("Email already registered");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Phone = request.Phone,
            Role = request.Role,
            IsVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User registered successfully: {Email}", request.Email);

        return user;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new InvalidOperationException("Invalid email or password");

        if (!user.IsActive)
            throw new InvalidOperationException("User account is inactive");

        user.LastLoginAt = DateTime.UtcNow;

        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id, user.Email, user.FullName, user.Role.ToString());
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

        // Store only a hash of the refresh token so a database read cannot leak usable tokens.
        user.RefreshToken = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);

        return new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IsVerified = user.IsVerified,
            TripNestId = user.TripNestId
        };
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(HashRefreshToken(refreshToken));

        if (user == null)
            throw new InvalidOperationException("Invalid or expired refresh token");

        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id, user.Email, user.FullName, user.Role.ToString());
        var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync();

        user.RefreshToken = HashRefreshToken(newRefreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Token refreshed for user: {Email}", user.Email);

        return new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            IsVerified = user.IsVerified,
            TripNestId = user.TripNestId
        };
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            throw new InvalidOperationException("New passwords do not match");

        PasswordPolicy.Validate(request.NewPassword);

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new InvalidOperationException("Current password is incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password changed for user: {Email}", user.Email);
    }

    public async Task<(User? User, string? ResetToken)> ForgotPasswordAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);

        // Do not reveal whether the email is registered — the controller always responds
        // with the same generic message regardless of the outcome here.
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for unknown email: {Email}", email);
            return (null, null);
        }

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(rawToken);
        user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        // TODO: send email with rawToken to user.Email via email provider (e.g. SendGrid / SMTP)
        _logger.LogInformation("Password reset token generated for {Email}. Token (dev-only): {Token}", email, rawToken);

        return (user, rawToken);
    }

    public async Task ResetPasswordAsync(string email, string resetToken, string newPassword)
    {
        var user = await _userRepository.GetByEmailAsync(email)
            ?? throw new InvalidOperationException("User not found");

        if (string.IsNullOrEmpty(user.PasswordResetToken) || user.PasswordResetTokenExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("Reset token is invalid or has expired");

        if (!BCrypt.Net.BCrypt.Verify(resetToken, user.PasswordResetToken))
            throw new InvalidOperationException("Reset token is invalid or has expired");

        PasswordPolicy.Validate(newPassword);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken = null;
        user.PasswordResetTokenExpiry = null;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password reset for user: {Email}", email);
    }

    /// <summary>
    /// Deterministic SHA-256 hash (hex) of a refresh token. Refresh tokens are
    /// high-entropy random values, so a fast hash is sufficient and keeps the
    /// stored value non-reversible while remaining queryable by equality.
    /// </summary>
    private static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
