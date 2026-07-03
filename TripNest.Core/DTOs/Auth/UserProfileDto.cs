using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Auth;

public class UserProfileDto
{
    public required string UserId { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required UserRole Role { get; set; }

    public bool IsVerified { get; set; }

    public bool EmailVerified { get; set; }

    public bool PhoneVerified { get; set; }

    public string? TripNestId { get; set; }
}

/// <summary>Partial profile update — omitted (null) fields keep their current value.</summary>
public class UpdateProfileRequest
{
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public string? Bio { get; set; }

    public string? Username { get; set; }
}
