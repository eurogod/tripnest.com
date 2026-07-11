namespace TripNest.Core.Interfaces.Services;

public record StudentStatusResponse(
    string? StudentEmail,
    bool IsVerifiedStudent,
    DateTime? VerifiedAt,
    DateTime? ExpiresAt);

/// <summary>
/// Student status via academic-email OTP: the user proves control of a university mailbox
/// (domain-checked against Student:AcademicDomainSuffixes), which unlocks the student discount
/// on Student-stayType listings for Student:ValidityDays.
/// </summary>
public interface IStudentVerificationService
{
    /// <summary>Sends a code to the given academic email (rejects non-academic domains).</summary>
    Task SendOtpAsync(string userId, string studentEmail);
    /// <summary>Confirms the code; on success the user is a verified student from now.</summary>
    Task<bool> VerifyOtpAsync(string userId, string code);
    Task<StudentStatusResponse> GetStatusAsync(string userId);
    /// <summary>Verified and not yet expired — the flag pricing consults.</summary>
    Task<bool> IsActiveStudentAsync(string userId);
}
