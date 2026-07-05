using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string Phone { get; set; }

    public required UserRole Role { get; set; }

    public bool IsVerified { get; set; } = false;

    public string? TripNestId { get; set; }

    public string? ProfilePhotoPath { get; set; }

    // Optional public display handle / nickname the user can set.
    public string? Username { get; set; }

    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Updated when the user's last chat connection drops; used to show "last seen" for offline users.
    public DateTime? LastSeenAt { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }

    // Brute-force protection: consecutive failed logins and, once the threshold is hit, the time
    // until which further login attempts are refused. Reset on a successful login.
    public int FailedLoginAttempts { get; set; }

    public DateTime? LockoutEnd { get; set; }

    // Phone-ownership (OTP) verification.
    public bool PhoneVerified { get; set; } = false;
    public string? PhoneOtpHash { get; set; }
    public DateTime? PhoneOtpExpiry { get; set; }
    public int PhoneOtpAttempts { get; set; }

    // Email-ownership (OTP) verification — independent of phone; either or both may be used.
    public bool EmailVerified { get; set; } = false;
    public string? EmailOtpHash { get; set; }
    public DateTime? EmailOtpExpiry { get; set; }
    public int EmailOtpAttempts { get; set; }

    // Saved trusted contact for safe-arrival check-ins (overridable per check-in request).
    public string? TrustedContactName { get; set; }
    public string? TrustedContactPhone { get; set; }
    public string? TrustedContactEmail { get; set; }
}
