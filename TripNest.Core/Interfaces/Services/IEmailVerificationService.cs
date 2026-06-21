namespace TripNest.Core.Interfaces.Services;

public interface IEmailVerificationService
{
    /// <summary>Generates a one-time code, stores its hash, and emails it to the user's address.
    /// Throttled and capped.</summary>
    Task SendOtpAsync(string userId);

    /// <summary>Checks a submitted code; on success marks the user's email verified.
    /// Returns false on a wrong/expired code.</summary>
    Task<bool> VerifyOtpAsync(string userId, string code);
}
