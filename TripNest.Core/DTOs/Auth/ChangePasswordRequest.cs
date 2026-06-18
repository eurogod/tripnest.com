namespace TripNest.Core.DTOs.Auth;

public class ChangePasswordRequest
{
    public required string CurrentPassword { get; set; }

    public required string NewPassword { get; set; }

    public required string ConfirmNewPassword { get; set; }
}
