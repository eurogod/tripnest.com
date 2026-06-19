using TripNest.Core.DTOs.Verification;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class VerificationService : IVerificationService
{
    private readonly IVerificationRepository _verificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INiaClient _niaClient;
    private readonly IFaceMatchClient _faceMatchClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        IVerificationRepository verificationRepository,
        IUserRepository userRepository,
        INiaClient niaClient,
        IFaceMatchClient faceMatchClient,
        IConfiguration configuration,
        ILogger<VerificationService> logger)
    {
        _verificationRepository = verificationRepository;
        _userRepository = userRepository;
        _niaClient = niaClient;
        _faceMatchClient = faceMatchClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VerificationStatusResponse> StartVerificationAsync(string userId, StartVerificationRequest request)
    {
        var threshold = _configuration.GetValue<double>("Verification:FaceMatchThreshold", 80.0);

        // Guard: an already-verified user should not re-run verification.
        var latest = await _verificationRepository.GetLatestByUserIdAsync(userId);
        if (latest?.Status == VerificationStatus.Verified)
            throw new InvalidOperationException("This account is already verified");

        // Rate limit: cap attempts per user per hour to protect the paid NIA / face-match calls from abuse.
        var maxAttemptsPerHour = _configuration.GetValue<int>("Verification:MaxAttemptsPerHour", 5);
        var attemptsLastHour = await _verificationRepository.CountAttemptsSinceAsync(userId, DateTime.UtcNow.AddHours(-1));
        if (attemptsLastHour >= maxAttemptsPerHour)
            throw new InvalidOperationException("Too many verification attempts. Please try again later.");

        var nia = await _niaClient.VerifyGhanaCardAsync(request.GhanaCardNumber);

        if (!nia.IsValid)
            throw new InvalidOperationException($"Ghana card could not be verified (status: {nia.Status})");

        // Defense in depth: ensure the card actually belongs to the person claiming it.
        if (!IdentityMatches(nia, request))
            throw new InvalidOperationException("The card details do not match the provided identity");

        var (faceMatchScore, failureReason) = await _faceMatchClient.CompareFacesAsync(request.SelfiePhotoPath, nia.PhotoUrl ?? string.Empty);

        var isVerified = string.IsNullOrEmpty(failureReason) && faceMatchScore >= threshold;

        var verification = new VerificationRequest
        {
            UserId = userId,
            GhanaCardNumber = request.GhanaCardNumber,
            SelfiePhotoPath = request.SelfiePhotoPath,
            NiaPhotoUrl = nia.PhotoUrl ?? string.Empty,
            FaceMatchScore = faceMatchScore,
            FailureReason = failureReason,
            Status = isVerified ? VerificationStatus.Verified : VerificationStatus.Rejected,
            ReviewedAt = DateTime.UtcNow
        };

        await _verificationRepository.AddAsync(verification);
        await _verificationRepository.SaveChangesAsync();

        if (isVerified)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && !user.IsVerified)
            {
                var sequence = await _verificationRepository.GetVerifiedCountAsync();
                user.IsVerified = true;
                user.TripNestId = $"TN-GH-{DateTime.UtcNow.Year}-{sequence:D6}";
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }
        }

        _logger.LogInformation("Verification completed for user {UserId} — score: {Score}, status: {Status}", userId, faceMatchScore, verification.Status);

        return MapToResponse(verification);
    }

    public async Task<VerificationStatusResponse> GetVerificationStatusAsync(string userId)
    {
        var verification = await _verificationRepository.GetLatestByUserIdAsync(userId)
            ?? throw new InvalidOperationException("No verification request found for this user");

        return MapToResponse(verification);
    }

    /// <summary>
    /// Confirms the authority's record matches the claimed identity: DOB exact (when known)
    /// and both the first and last name appearing in the registered full name.
    /// </summary>
    private static bool IdentityMatches(NiaVerificationResult nia, StartVerificationRequest request)
    {
        if (nia.DateOfBirth.HasValue && nia.DateOfBirth.Value != request.DateOfBirth)
            return false;

        if (!string.IsNullOrWhiteSpace(nia.FullName))
        {
            var fullName = nia.FullName!.ToLowerInvariant();
            if (!fullName.Contains(request.FirstName.Trim().ToLowerInvariant())
                || !fullName.Contains(request.LastName.Trim().ToLowerInvariant()))
                return false;
        }

        return true;
    }

    private static VerificationStatusResponse MapToResponse(VerificationRequest verification) => new()
    {
        VerificationId = verification.Id,
        GhanaCardNumber = MaskCardNumber(verification.GhanaCardNumber),
        Status = verification.Status,
        FaceMatchScore = verification.FaceMatchScore,
        FailureReason = verification.FailureReason,
        SubmittedAt = verification.SubmittedAt,
        ReviewedAt = verification.ReviewedAt
    };

    /// <summary>Masks all but the last 4 characters of the card number so PII is not echoed back in full.</summary>
    private static string MaskCardNumber(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length <= 4)
            return "****";

        return string.Concat(new string('*', cardNumber.Length - 4), cardNumber.AsSpan(cardNumber.Length - 4));
    }
}
