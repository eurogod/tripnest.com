using TripNest.Core.DTOs.Verification;

namespace TripNest.Core.Interfaces.Services;

public interface IVerificationService
{
    Task<VerificationStatusResponse> StartVerificationAsync(string userId, StartVerificationRequest request);
    Task<VerificationStatusResponse> GetVerificationStatusAsync(string userId);

    /// <summary>
    /// Runs the NIA lookup + face match for a queued (Pending) verification request and
    /// resolves it to Verified/Rejected. Invoked by the background processor, not the HTTP path.
    /// </summary>
    Task ProcessVerificationAsync(string verificationId);
}
