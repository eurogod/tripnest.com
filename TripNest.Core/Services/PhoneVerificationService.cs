using System.Security.Cryptography;
using System.Text;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Self-managed phone OTP: generates a 6-digit code, stores only its SHA-256 hash with a short
/// expiry, and sends it over SMS. Verification is constant-time, single-use, and attempt-capped
/// to resist brute force. Works with the sender's graceful fallback — when no provider is
/// configured the code is logged so dev/test flows still work.
/// </summary>
public class PhoneVerificationService : IPhoneVerificationService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    // Minimum gap between consecutive sends, so a user can't trigger a burst of SMS.
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IUserRepository _userRepository;
    private readonly ISmsSender _smsSender;
    private readonly IPhoneNumberValidator _phoneValidator;
    private readonly ILogger<PhoneVerificationService> _logger;

    public PhoneVerificationService(
        IUserRepository userRepository,
        ISmsSender smsSender,
        IPhoneNumberValidator phoneValidator,
        ILogger<PhoneVerificationService> logger)
    {
        _userRepository = userRepository;
        _smsSender = smsSender;
        _phoneValidator = phoneValidator;
        _logger = logger;
    }

    public async Task SendOtpAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        if (user.PhoneVerified)
            throw new InvalidOperationException("Phone number is already verified");

        // Resend cooldown: lastSentAt is derived from the stored expiry (= lastSentAt + Ttl),
        // so no extra column is needed to make the user wait between sends.
        if (user.PhoneOtpExpiry is { } expiry)
        {
            var wait = (expiry - Ttl) + ResendCooldown - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                throw new TooManyRequestsException(
                    $"Please wait {Math.Ceiling(wait.TotalSeconds)}s before requesting another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.PhoneOtpHash = Hash(code);
        user.PhoneOtpExpiry = DateTime.UtcNow.Add(Ttl);
        user.PhoneOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var message = $"Your TripNest verification code is {code}. It expires in 10 minutes.";
        var sent = await _smsSender.SendSmsAsync(user.Phone, message);

        if (!sent)
            _logger.LogInformation("[OTP not delivered — provider unconfigured] code for {UserId}: {Code}", userId, code);
    }

    public async Task<bool> VerifyOtpAsync(string userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        if (user.PhoneVerified)
            return true;

        if (user.PhoneOtpHash == null || user.PhoneOtpExpiry == null || user.PhoneOtpExpiry < DateTime.UtcNow)
            throw new InvalidOperationException("No active code. Please request a new one.");
        if (user.PhoneOtpAttempts >= MaxAttempts)
            throw new InvalidOperationException("Too many attempts. Please request a new code.");

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code ?? string.Empty)),
            Encoding.UTF8.GetBytes(user.PhoneOtpHash));

        if (!matches)
        {
            user.PhoneOtpAttempts++;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
            return false;
        }

        user.PhoneVerified = true;
        user.PhoneOtpHash = null;
        user.PhoneOtpExpiry = null;
        user.PhoneOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Phone verified for user {UserId}", userId);
        return true;
    }

    public async Task SendLoginOtpAsync(string phone)
    {
        var user = await FindLoginUserAsync(phone);
        if (user is null)
        {
            // Unknown, inactive, or ambiguous number — say nothing to the caller (the endpoint
            // returns the same generic response either way, so phones can't be enumerated).
            _logger.LogInformation("Phone-login code requested for an unknown or ambiguous phone number");
            return;
        }

        // Enumeration safety: unlike the authenticated flow, the resend cooldown is enforced
        // silently (no 429) — a 429 would reveal the number is registered. The endpoint's "otp"
        // rate limit still bounds how often anyone can call this.
        if (user.PhoneOtpExpiry is { } expiry)
        {
            var wait = (expiry - Ttl) + ResendCooldown - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                _logger.LogInformation("Phone-login code resend suppressed by cooldown for user {UserId}", user.Id);
                return;
            }
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.PhoneOtpHash = Hash(code);
        user.PhoneOtpExpiry = DateTime.UtcNow.Add(Ttl);
        user.PhoneOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var message = $"Your TripNest login code is {code}. It expires in 10 minutes.";
        var sent = await _smsSender.SendSmsAsync(user.Phone, message);

        if (!sent)
            _logger.LogInformation("[OTP not delivered — provider unconfigured] login code for {UserId}: {Code}", user.Id, code);
    }

    public async Task<string?> VerifyLoginOtpAsync(string phone, string code)
    {
        var user = await FindLoginUserAsync(phone);

        // Unlike VerifyOtpAsync, never short-circuit on PhoneVerified — this code IS the login
        // credential, so it must be checked every time. All failures collapse to null so the
        // endpoint can return one generic error.
        if (user?.PhoneOtpHash is null || user.PhoneOtpExpiry is null || user.PhoneOtpExpiry < DateTime.UtcNow)
            return null;
        if (user.PhoneOtpAttempts >= MaxAttempts)
            return null;

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code ?? string.Empty)),
            Encoding.UTF8.GetBytes(user.PhoneOtpHash));

        if (!matches)
        {
            user.PhoneOtpAttempts++;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
            return null;
        }

        // Single-use: consume the code. Possession of the phone also proves ownership.
        user.PhoneVerified = true;
        user.PhoneOtpHash = null;
        user.PhoneOtpExpiry = null;
        user.PhoneOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Phone login verified for user {UserId}", user.Id);
        return user.Id;
    }

    /// <summary>
    /// Resolves a phone number to the single active account owning it. Phone is not unique in the
    /// schema, so an ambiguous number (shared by several accounts) cannot be used to sign in.
    /// </summary>
    private async Task<Models.User?> FindLoginUserAsync(string phone)
    {
        var normalized = _phoneValidator.Normalize(phone);
        if (normalized is null)
            return null;

        var matches = (await _userRepository.FindAsync(u => u.Phone == normalized && u.IsActive)).ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
