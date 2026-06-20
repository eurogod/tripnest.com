using System.Security.Cryptography;
using System.Text;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Self-managed phone OTP: generates a 6-digit code, stores only its SHA-256 hash with a short
/// expiry, and sends it over SMS or WhatsApp. Verification is constant-time, single-use, and
/// attempt-capped to resist brute force. Works with the senders' graceful fallback — when no
/// provider is configured the code is logged so dev/test flows still work.
/// </summary>
public class PhoneVerificationService : IPhoneVerificationService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IUserRepository _userRepository;
    private readonly ISmsSender _smsSender;
    private readonly IWhatsAppSender _whatsAppSender;
    private readonly ILogger<PhoneVerificationService> _logger;

    public PhoneVerificationService(
        IUserRepository userRepository,
        ISmsSender smsSender,
        IWhatsAppSender whatsAppSender,
        ILogger<PhoneVerificationService> logger)
    {
        _userRepository = userRepository;
        _smsSender = smsSender;
        _whatsAppSender = whatsAppSender;
        _logger = logger;
    }

    public async Task SendOtpAsync(string userId, string channel)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new InvalidOperationException("User not found");
        if (user.PhoneVerified)
            throw new InvalidOperationException("Phone number is already verified");

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.PhoneOtpHash = Hash(code);
        user.PhoneOtpExpiry = DateTime.UtcNow.Add(Ttl);
        user.PhoneOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var message = $"Your TripNest verification code is {code}. It expires in 10 minutes.";
        var sent = string.Equals(channel, "whatsapp", StringComparison.OrdinalIgnoreCase)
            ? await _whatsAppSender.SendAsync(user.Phone, message)
            : await _smsSender.SendSmsAsync(user.Phone, message);

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

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
