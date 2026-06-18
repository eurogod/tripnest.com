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
        try
        {
            var threshold = _configuration.GetValue<double>("Verification:FaceMatchThreshold", 80.0);

            var (isValid, photoUrl) = await _niaClient.VerifyGhanaCardAsync(
                request.GhanaCardNumber,
                request.FirstName,
                request.LastName,
                request.DateOfBirth);

            if (!isValid)
                throw new InvalidOperationException("Ghana card verification failed with NIA service");

            var (faceMatchScore, failureReason) = await _faceMatchClient.CompareFacesAsync(request.SelfiePhotoPath, photoUrl);

            var isVerified = string.IsNullOrEmpty(failureReason) && faceMatchScore >= threshold;

            var verification = new VerificationRequest
            {
                UserId = userId,
                GhanaCardNumber = request.GhanaCardNumber,
                SelfiePhotoPath = request.SelfiePhotoPath,
                NiaPhotoUrl = photoUrl,
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

            return new VerificationStatusResponse
            {
                VerificationId = verification.Id,
                GhanaCardNumber = verification.GhanaCardNumber,
                Status = verification.Status,
                FaceMatchScore = verification.FaceMatchScore,
                FailureReason = verification.FailureReason,
                SubmittedAt = verification.SubmittedAt,
                ReviewedAt = verification.ReviewedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting verification for user {UserId}", userId);
            throw;
        }
    }

    public async Task<VerificationStatusResponse> GetVerificationStatusAsync(string userId)
    {
        try
        {
            var verification = await _verificationRepository.GetLatestByUserIdAsync(userId);

            if (verification == null)
                throw new InvalidOperationException("No verification request found for this user");

            return new VerificationStatusResponse
            {
                VerificationId = verification.Id,
                GhanaCardNumber = verification.GhanaCardNumber,
                Status = verification.Status,
                FaceMatchScore = verification.FaceMatchScore,
                FailureReason = verification.FailureReason,
                SubmittedAt = verification.SubmittedAt,
                ReviewedAt = verification.ReviewedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving verification status for user {UserId}", userId);
            throw;
        }
    }
}
