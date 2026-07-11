using System.Security.Cryptography;
using System.Text;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Academic-email OTP, mirroring <see cref="EmailVerificationService"/> (hashed single-use code,
/// short expiry, attempt cap, resend cooldown) — but sent to a SEPARATE university mailbox rather
/// than the account email, and gated on the domain actually being academic. Verification expires
/// after Student:ValidityDays (default 365) so graduates age out; re-verifying any time renews it.
/// </summary>
public class StudentVerificationService : IStudentVerificationService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private static readonly string[] DefaultAcademicSuffixes = { ".edu", ".edu.gh", ".ac.gh" };

    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StudentVerificationService> _logger;

    public StudentVerificationService(
        IUserRepository userRepository,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<StudentVerificationService> logger)
    {
        _userRepository = userRepository;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    private int ValidityDays => _configuration.GetValue("Student:ValidityDays", 365);

    private string[] AcademicSuffixes =>
        _configuration.GetSection("Student:AcademicDomainSuffixes").Get<string[]>() is { Length: > 0 } configured
            ? configured
            : DefaultAcademicSuffixes;

    public async Task SendOtpAsync(string userId, string studentEmail)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");

        studentEmail = (studentEmail ?? "").Trim();
        var at = studentEmail.LastIndexOf('@');
        if (at <= 0 || at == studentEmail.Length - 1)
            throw new ValidationException("Provide a valid student email address");

        // The whole point of the check: only mailboxes a university actually issues qualify.
        var domain = studentEmail[(at + 1)..].ToLowerInvariant();
        var isAcademic = AcademicSuffixes.Any(s =>
        {
            var suffix = s.ToLowerInvariant();
            return domain.EndsWith(suffix, StringComparison.Ordinal) ||
                   domain == suffix.TrimStart('.');
        });
        if (!isAcademic)
            throw new ValidationException(
                "That doesn't look like a university email. Use your school-issued address (e.g. you@st.ug.edu.gh).");

        // Same resend-cooldown derivation as the account-email OTP (no extra column needed).
        if (user.StudentOtpExpiry is { } expiry)
        {
            var wait = (expiry - Ttl) + ResendCooldown - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                throw new TooManyRequestsException(
                    $"Please wait {Math.Ceiling(wait.TotalSeconds)}s before requesting another code.");
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        user.PendingStudentEmail = studentEmail;
        user.StudentOtpHash = Hash(code);
        user.StudentOtpExpiry = DateTime.UtcNow.Add(Ttl);
        user.StudentOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        var sent = await _emailSender.SendAsync(studentEmail,
            "Verify your student status on TripNest",
            $"<p>Your TripNest student verification code is <strong>{code}</strong>.</p>" +
            "<p>It expires in 10 minutes. Verifying unlocks student rates on student housing.</p>");

        if (!sent)
            _logger.LogInformation("[Student OTP not delivered — provider unconfigured] code for {UserId}: {Code}", userId, code);
    }

    public async Task<bool> VerifyOtpAsync(string userId, string code)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");

        if (user.StudentOtpHash == null || user.StudentOtpExpiry == null || user.StudentOtpExpiry < DateTime.UtcNow)
            throw new ValidationException("No active code. Please request a new one.");
        if (user.StudentOtpAttempts >= MaxAttempts)
            throw new ValidationException("Too many attempts. Please request a new code.");

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Hash(code ?? string.Empty)),
            Encoding.UTF8.GetBytes(user.StudentOtpHash));

        if (!matches)
        {
            user.StudentOtpAttempts++;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
            return false;
        }

        user.StudentEmail = user.PendingStudentEmail;
        user.StudentVerifiedAt = DateTime.UtcNow;
        user.PendingStudentEmail = null;
        user.StudentOtpHash = null;
        user.StudentOtpExpiry = null;
        user.StudentOtpAttempts = 0;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        _logger.LogInformation("Student status verified for user {UserId} ({Email})", userId, user.StudentEmail);
        return true;
    }

    public async Task<StudentStatusResponse> GetStatusAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");
        var expiresAt = user.StudentVerifiedAt?.AddDays(ValidityDays);
        return new StudentStatusResponse(
            user.StudentEmail,
            expiresAt.HasValue && expiresAt > DateTime.UtcNow,
            user.StudentVerifiedAt,
            expiresAt);
    }

    public async Task<bool> IsActiveStudentAsync(string userId) =>
        (await GetStatusAsync(userId)).IsVerifiedStudent;

    private static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
