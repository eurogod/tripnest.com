using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Auth;

public class UserProfileDto
{
    public required string UserId { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required UserRole Role { get; set; }

    public bool IsVerified { get; set; }

    public string? TripNestId { get; set; }
}
