using Microsoft.EntityFrameworkCore;
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
    private readonly INotificationService _notificationService;
    private readonly IVerificationQueue _verificationQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VerificationService> _logger;

    public VerificationService(
        IVerificationRepository verificationRepository,
        IUserRepository userRepository,
        INiaClient niaClient,
        IFaceMatchClient faceMatchClient,
        INotificationService notificationService,
        IVerificationQueue verificationQueue,
        IConfiguration configuration,
        ILogger<VerificationService> logger)
    {
        _verificationRepository = verificationRepository;
        _userRepository = userRepository;
        _niaClient = niaClient;
        _faceMatchClient = faceMatchClient;
        _notificationService = notificationService;
        _verificationQueue = verificationQueue;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Accepts a verification submission, persists it as <see cref="VerificationStatus.Pending"/>,
    /// queues it for background processing, and returns immediately. The slow NIA + face-match
    /// work happens off the request path (see <see cref="ProcessVerificationAsync"/>), so the
    /// client can advance to the next page and poll <c>GetVerificationStatus</c> for the outcome.
    /// </summary>
    public async Task<VerificationStatusResponse> StartVerificationAsync(string userId, StartVerificationRequest request)
    {
        // Guard: an already-verified user should not re-run verification.
        var latest = await _verificationRepository.GetLatestByUserIdAsync(userId);
        if (latest?.Status == VerificationStatus.Verified)
            throw new InvalidOperationException("This account is already verified");

        // Guard: a submission is already in flight — don't queue duplicates.
        if (latest?.Status == VerificationStatus.Pending)
            return MapToResponse(latest);

        // Rate limit: cap attempts per user per hour to protect the paid NIA / face-match calls from abuse.
        var maxAttemptsPerHour = _configuration.GetValue<int>("Verification:MaxAttemptsPerHour", 5);
        var attemptsLastHour = await _verificationRepository.CountAttemptsSinceAsync(userId, DateTime.UtcNow.AddHours(-1));
        if (attemptsLastHour >= maxAttemptsPerHour)
            throw new InvalidOperationException("Too many verification attempts. Please try again later.");

        var verification = new VerificationRequest
        {
            UserId = userId,
            GhanaCardNumber = request.GhanaCardNumber,
            SelfiePhotoPath = request.SelfiePhotoPath,
            NiaPhotoUrl = string.Empty, // resolved by the background processor from the NIA lookup
            ClaimedFirstName = request.FirstName,
            ClaimedLastName = request.LastName,
            ClaimedDateOfBirth = request.DateOfBirth,
            Status = VerificationStatus.Pending
        };

        await _verificationRepository.AddAsync(verification);
        await _verificationRepository.SaveChangesAsync();

        _verificationQueue.Enqueue(verification.Id);

        _logger.LogInformation("Verification {VerificationId} queued for user {UserId}", verification.Id, userId);

        return MapToResponse(verification);
    }

    /// <summary>
    /// Background work: resolve a Pending verification by calling the NIA authority and the
    /// face-match sidecar, then mark it Verified/Rejected. On completion the user is notified
    /// (success, or failure with a retry prompt). Runs inside a dedicated DI scope created by
    /// the hosted processor.
    /// </summary>
    public async Task ProcessVerificationAsync(string verificationId)
    {
        var verification = await _verificationRepository.GetByIdAsync(verificationId);
        if (verification == null)
        {
            _logger.LogWarning("Verification {VerificationId} not found for processing", verificationId);
            return;
        }

        if (verification.Status != VerificationStatus.Pending)
        {
            _logger.LogInformation("Verification {VerificationId} already resolved ({Status}); skipping", verificationId, verification.Status);
            return;
        }

        var threshold = _configuration.GetValue<double>("Verification:FaceMatchThreshold", 80.0);
        string? failureReason = null;
        double? faceMatchScore = null;
        var isVerified = false;

        try
        {
            var nia = await _niaClient.VerifyGhanaCardAsync(verification.GhanaCardNumber);

            if (!nia.IsValid)
            {
                failureReason = $"Ghana card could not be verified (status: {nia.Status})";
            }
            else if (!IdentityMatches(nia, verification))
            {
                failureReason = "The card details do not match the provided identity";
            }
            else
            {
                verification.NiaPhotoUrl = nia.PhotoUrl ?? string.Empty;
                var (score, matchFailure) = await _faceMatchClient.CompareFacesAsync(verification.SelfiePhotoPath, nia.PhotoUrl ?? string.Empty);
                faceMatchScore = score;
                failureReason = matchFailure;
                isVerified = string.IsNullOrEmpty(matchFailure) && score >= threshold;
                if (!isVerified && string.IsNullOrEmpty(failureReason))
                    failureReason = $"Face match score {score:F0} is below the required {threshold:F0}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verification {VerificationId} failed during processing", verificationId);
            failureReason = "Verification could not be completed. Please try again.";
        }

        verification.FaceMatchScore = faceMatchScore;
        verification.FailureReason = failureReason;
        verification.Status = isVerified ? VerificationStatus.Verified : VerificationStatus.Rejected;
        verification.ReviewedAt = DateTime.UtcNow;
        await _verificationRepository.UpdateAsync(verification);

        // Mark the user verified and mint their TripNestId in the SAME save as the verification
        // outcome, so we never end up with a Verified request but an unbadged user.
        User? userToBadge = null;
        if (isVerified)
        {
            var user = await _userRepository.GetByIdAsync(verification.UserId);
            if (user != null && !user.IsVerified)
            {
                user.IsVerified = true;
                userToBadge = user; // TripNestId assigned inside the collision-safe save below
            }
        }

        await PersistOutcomeWithTripNestIdAsync(userToBadge);

        if (isVerified)
        {
            await _notificationService.NotifyAsync(
                verification.UserId,
                Enums.NotificationType.VerificationStatusChanged,
                "Identity verified",
                "Your Ghana Card has been verified — your account is now fully verified.");
        }
        else
        {
            await _notificationService.NotifyAsync(
                verification.UserId,
                Enums.NotificationType.VerificationStatusChanged,
                "Verification failed",
                "We couldn't verify your identity. Tap to try again.");
        }

        _logger.LogInformation("Verification {VerificationId} resolved — score: {Score}, status: {Status}",
            verificationId, faceMatchScore, verification.Status);
    }

    /// <summary>
    /// Saves the verification outcome and, when a user is being badged, assigns their TripNestId in
    /// the same atomic save. The serial is the count of already-issued IDs + 1; under concurrency two
    /// users can briefly compute the same serial, so a unique-index violation is caught and retried
    /// with a freshly recomputed serial. The repositories share one DbContext, so a single
    /// SaveChanges commits both the verification and the user together.
    /// </summary>
    private async Task PersistOutcomeWithTripNestIdAsync(User? userToBadge)
    {
        if (userToBadge == null)
        {
            await _verificationRepository.SaveChangesAsync();
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            userToBadge.TripNestId = TripNestIdGenerator.Format(
                await _userRepository.CountAssignedTripNestIdsAsync() + 1);
            try
            {
                await _userRepository.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                _logger.LogWarning("TripNestId collision assigning to user {UserId} (attempt {Attempt}); retrying",
                    userToBadge.Id, attempt);
            }
        }
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
    private static bool IdentityMatches(NiaVerificationResult nia, VerificationRequest verification)
    {
        if (nia.DateOfBirth.HasValue && verification.ClaimedDateOfBirth.HasValue
            && nia.DateOfBirth.Value != verification.ClaimedDateOfBirth.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(nia.FullName))
        {
            var fullName = nia.FullName!.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(verification.ClaimedFirstName)
                && !fullName.Contains(verification.ClaimedFirstName!.Trim().ToLowerInvariant()))
                return false;
            if (!string.IsNullOrWhiteSpace(verification.ClaimedLastName)
                && !fullName.Contains(verification.ClaimedLastName!.Trim().ToLowerInvariant()))
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
