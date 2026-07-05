namespace TripNest.Core.Interfaces.Services;

public interface IPhoneVerificationService
{
    /// <summary>Generates a one-time code, stores its hash, and sends it to the user's phone
    /// via SMS. Throttled and capped.</summary>
    Task SendOtpAsync(string userId);

    /// <summary>Checks a submitted code; on success marks the user's phone verified.
    /// Returns false on a wrong/expired code.</summary>
    Task<bool> VerifyOtpAsync(string userId, string code);

    /// <summary>
    /// Anonymous phone-login step 1: sends a one-time code to the phone number if it belongs to
    /// exactly one active account. Deliberately silent on unknown/ambiguous numbers and resend
    /// cooldowns so callers can't probe which phones are registered.
    /// </summary>
    Task SendLoginOtpAsync(string phone);

    /// <summary>
    /// Anonymous phone-login step 2: checks the code for the account owning the phone number.
    /// Returns the user id on success (also marking the phone verified — the code proves
    /// ownership), or null for any failure.
    /// </summary>
    Task<string?> VerifyLoginOtpAsync(string phone, string code);
}
