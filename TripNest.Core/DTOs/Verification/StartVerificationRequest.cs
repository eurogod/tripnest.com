namespace TripNest.Core.DTOs.Verification;

public class StartVerificationRequest
{
    public required string GhanaCardNumber { get; set; }
    public required string SelfiePhotoPath { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required DateOnly DateOfBirth { get; set; }
}
