using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Auth;

public class LoginResponse
{
    public required string UserId { get; set; }

    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required UserRole Role { get; set; }

    public required string AccessToken { get; set; }

    public required string RefreshToken { get; set; }

    public bool IsVerified { get; set; }

    public bool EmailVerified { get; set; }

    public bool PhoneVerified { get; set; }

    public string? TripNestId { get; set; }
}
