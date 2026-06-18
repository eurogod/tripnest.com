using TripNest.Core.DTOs.Verification;

namespace TripNest.Core.Interfaces.Services;

public interface IVerificationService
{
    Task<VerificationStatusResponse> StartVerificationAsync(string userId, StartVerificationRequest request);
    Task<VerificationStatusResponse> GetVerificationStatusAsync(string userId);
}
