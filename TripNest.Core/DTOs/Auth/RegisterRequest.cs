using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Auth;

public class RegisterRequest
{
    public required string FullName { get; set; }

    public required string Email { get; set; }

    public required string Password { get; set; }

    public required string ConfirmPassword { get; set; }

    public required string Phone { get; set; }

    public required UserRole Role { get; set; }
}
