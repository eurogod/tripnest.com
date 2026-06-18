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

    public string? Bio { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiryTime { get; set; }

    public string? PasswordResetToken { get; set; }

    public DateTime? PasswordResetTokenExpiry { get; set; }
}
