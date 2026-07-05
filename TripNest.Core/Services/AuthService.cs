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
    private readonly IPhoneNumberValidator _phoneValidator;
    private readonly IPhoneVerificationService _phoneVerification;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPhoneNumberValidator phoneValidator,
        IPhoneVerificationService phoneVerification,
        IEmailSender emailSender,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _phoneValidator = phoneValidator;
        _phoneVerification = phoneVerification;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<User> RegisterAsync(RegisterRequest request)
    {
        if (request.Role == UserRole.Admin)
            throw new InvalidOperationException("Admin accounts cannot be self-registered");

        if (request.Password != request.ConfirmPassword)
            throw new InvalidOperationException("Passwords do not match");

        PasswordPolicy.Validate(request.Password);

        // Validate + normalise the phone number to E.164 so SMS delivery works.
        var normalizedPhone = _phoneValidator.Normalize(request.Phone)
            ?? throw new InvalidOperationException("Please provide a valid phone number");

        if (await _userRepository.EmailExistsAsync(request.Email))
            throw new InvalidOperationException("Email already registered");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHash,
            Phone = normalizedPhone,
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

    // Brute-force policy: after this many consecutive failures the account is locked for the window
    // below. A correct login before the threshold clears the counter, so honest users are unaffected.
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email)
            ?? throw new InvalidOperationException("Invalid email or password");

        // Refuse while locked out — don't even check the password, so a locked account can't be probed.
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Login blocked for locked-out account: {Email}", request.Email);
            throw new InvalidOperationException("Account temporarily locked due to too many failed attempts. Please try again later.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await RecordFailedLoginAsync(user);
            throw new InvalidOperationException("Invalid email or password");
        }

        if (!user.IsActive)
            throw new InvalidOperationException("User account is inactive");

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);

        return await SignInAndIssueTokensAsync(user);
    }

    /// <summary>
    /// Records a successful interactive sign-in — clears brute-force lockout state, stamps
    /// last-login — then issues the session. Shared by every authentication flow that proves
    /// the user's identity afresh (password, Google/Facebook, phone OTP); token refresh must
    /// NOT go through here as it is not a new proof of identity.
    /// </summary>
    private Task<LoginResponse> SignInAndIssueTokensAsync(User user)
    {
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        return IssueTokensAsync(user);
    }

    /// <summary>
    /// Issues a fresh access + refresh token pair, stores only the refresh token's hash (so a
    /// database read cannot leak usable tokens), and commits all staged changes in one save.
    /// The user is either tracked by the shared context or newly Added — deliberately no
    /// Update call, which would flip an Added entity to Modified and break provisioning.
    /// </summary>
    private async Task<LoginResponse> IssueTokensAsync(User user)
    {
        var accessToken = await _tokenService.GenerateAccessTokenAsync(
            user.Id, user.Email, user.FullName, user.Role.ToString());
        var refreshToken = await _tokenService.GenerateRefreshTokenAsync();

        user.RefreshToken = HashRefreshToken(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userRepository.SaveChangesAsync();

        return new LoginResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            IsVerified = user.IsVerified,
            EmailVerified = user.EmailVerified,
            PhoneVerified = user.PhoneVerified,
            TripNestId = user.TripNestId
        };
    }

    public async Task<LoginResponse> ExternalSignInAsync(string email, string fullName, bool emailVerified)
    {
        // The provider's email claim is only trustworthy once the provider itself has verified it
        // (Google ID tokens can carry email_verified=false). Signing in on an unverified claim would
        // let anyone who controls a Google identity asserting someone else's address take over the
        // existing TripNest account registered under that email.
        if (!emailVerified)
            throw new InvalidOperationException(
                "The Google account's email address is not verified. Verify it with Google and try again.");

        var user = await _userRepository.GetByEmailAsync(email);
        var isNewUser = user == null;
        if (user == null)
        {
            // First social sign-in: provision a Tenant account with an unusable random password
            // (they authenticate via the provider, not a password).
            user = new User
            {
                Id = Guid.NewGuid().ToString(),
                FullName = fullName,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32))),
                Phone = string.Empty,
                Role = UserRole.Tenant,
                IsVerified = false,
                IsActive = true,
                EmailVerified = emailVerified,
                CreatedAt = DateTime.UtcNow
            };
        }
        else if (!user.IsActive)
        {
            throw new InvalidOperationException("User account is inactive");
        }

        if (isNewUser)
        {
            await _userRepository.AddAsync(user);
            _logger.LogInformation("Provisioned new user from external sign-in: {Email}", email);
        }

        return await SignInAndIssueTokensAsync(user);
    }

    public async Task<LoginResponse> PhoneLoginAsync(string phone, string code)
    {
        // The OTP is the credential: possession of the phone signs the user in. All failure modes
        // (unknown/ambiguous phone, wrong, expired, or over-attempted code) collapse into one
        // generic error so the endpoint reveals nothing about which part failed.
        var userId = await _phoneVerification.VerifyLoginOtpAsync(phone, code)
            ?? throw new InvalidOperationException("Invalid or expired code");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("Invalid or expired code");

        _logger.LogInformation("User logged in via phone OTP: {UserId}", user.Id);

        return await SignInAndIssueTokensAsync(user);
    }

    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(HashRefreshToken(refreshToken));

        if (user == null)
            throw new InvalidOperationException("Invalid or expired refresh token");

        _logger.LogInformation("Token refreshed for user: {Email}", user.Email);

        // Deliberately not SignInAndIssueTokensAsync: a refresh is not a fresh proof of identity,
        // so it must not clear brute-force lockout state or count as a login.
        return await IssueTokensAsync(user);
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

        // Changing the password invalidates existing sessions: drop the refresh token so it
        // cannot be exchanged for new access tokens.
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password changed for user: {Email}", user.Email);
    }

    public async Task LogoutAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return; // Nothing to revoke — treat as success (idempotent).

        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("User logged out (refresh token revoked): {UserId}", userId);
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

        // Email the token to the address on file. Never log the raw token — it is a bearer
        // credential. IEmailSender degrades gracefully (logs + returns false) when SMTP is
        // unconfigured, so this never throws and the controller still returns its generic message.
        var subject = "Reset your TripNest password";
        var html = $"<p>We received a request to reset your TripNest password.</p>" +
                   $"<p>Use this code to reset it (valid for 1 hour):</p>" +
                   $"<p style=\"font-size:18px;font-weight:bold;letter-spacing:1px\">{rawToken}</p>" +
                   $"<p>If you didn't request this, you can safely ignore this email.</p>";
        await _emailSender.SendAsync(user.Email, subject, html);

        _logger.LogInformation("Password reset token generated and emailed for {Email}", email);

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

        // A reset (often used after a compromise) must also revoke existing sessions.
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Password reset for user: {Email}", email);
    }

    /// <summary>
    /// Records a failed login: increments the counter and, once it reaches the threshold, sets a
    /// time-boxed lockout. Persisted immediately so the count survives across requests/instances.
    /// </summary>
    private async Task RecordFailedLoginAsync(User user)
    {
        user.FailedLoginAttempts++;
        if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            user.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);
            user.FailedLoginAttempts = 0; // reset the counter; the lockout window now governs access
            _logger.LogWarning("Account locked after repeated failed logins: {Email}", user.Email);
        }

        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Deterministic SHA-256 hash (hex) of a refresh token. Refresh tokens are
    /// high-entropy random values, so a fast hash is sufficient and keeps the
    /// stored value non-reversible while remaining queryable by equality.
    /// </summary>
    private static string HashRefreshToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
