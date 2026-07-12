using System.Security.Cryptography;
using System.Text;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Self-managed email OTP: generates a 6-digit code, stores only its SHA-256 hash with a short
/// expiry, and emails it. Verification is constant-time, single-use, and attempt-capped to resist
/// brute force. Mirrors <see cref="PhoneVerificationService"/>. Works with the email sender's
/// graceful fallback — when SMTP isn't configured the code is logged so dev/test flows still work.
/// </summary>
public class EmailVerificationService : IEmailVerificationService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    // Minimum gap between consecutive sends, so a user can't trigger a burst of emails.
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailVerificationService> _logger;

    public EmailVerificationService(
        IUserRepository userRepository,
        IEmailSender emailSender,
        ILogger<EmailVerificationService> logger)
    {
        _userRepository = userRepository;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task SendOtpAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");
        if (user.EmailVerified)
            throw new ValidationException("Email address is already verified");

        // Resend cooldown: lastSentAt is derived from the stored expiry (= lastSentAt + Ttl),
        // so no extra column is needed to make the user wait between sends.
        if (user.EmailOtpExpiry is { } expiry)
        {
            var wait = (expiry - Ttl) + ResendCooldown - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                throw new TooManyRequestsException(
                    $"Please wait {Math.Ceiling(wait.TotalSeconds)}s before requesting another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.EmailOtpHash = Hash(code);
        user.EmailOtpExpiry = DateTime.UtcNow.Add(Ttl);
        user.EmailOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var subject = "Your TripNest verification code";
        var body = $"<p>Your TripNest verification code is <strong>{code}</strong>.</p>" +
                   "<p>It expires in 10 minutes.</p>";
        var sent = await _emailSender.SendAsync(user.Email, subject, body);

        if (!sent)
            _logger.LogInformation("[Email OTP not delivered — provider unconfigured] code for {UserId}: {Code}", userId, code);
    }

    public async Task<bool> VerifyOtpAsync(string userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");
        if (user.EmailVerified)
            return true;

        if (user.EmailOtpHash == null || user.EmailOtpExpiry == null || user.EmailOtpExpiry < DateTime.UtcNow)
            throw new ValidationException("No active code. Please request a new one.");
        if (user.EmailOtpAttempts >= MaxAttempts)
            throw new TooManyRequestsException("Too many attempts. Please request a new code.");

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code ?? string.Empty)),
            Encoding.UTF8.GetBytes(user.EmailOtpHash));

        if (!matches)
        {
            user.EmailOtpAttempts++;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
            return false;
        }

        user.EmailVerified = true;
        user.EmailOtpHash = null;
        user.EmailOtpExpiry = null;
        user.EmailOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Email verified for user {UserId}", userId);
        return true;
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
