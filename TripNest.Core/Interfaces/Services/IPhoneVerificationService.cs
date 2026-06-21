namespace TripNest.Core.Interfaces.Services;

public interface IPhoneVerificationService
{
    /// <summary>Generates a one-time code, stores its hash, and sends it to the user's phone
    /// via SMS. Throttled and capped.</summary>
    Task SendOtpAsync(string userId);

    /// <summary>Checks a submitted code; on success marks the user's phone verified.
    /// Returns false on a wrong/expired code.</summary>
    Task<bool> VerifyOtpAsync(string userId, string code);
}
