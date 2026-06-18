namespace TripNest.Core.DTOs.Auth;

public class ResetPasswordRequest
{
    public required string Email { get; set; }

    public required string ResetToken { get; set; }

    public required string NewPassword { get; set; }

    public required string ConfirmPassword { get; set; }
}
