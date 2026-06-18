namespace TripNest.Core.Interfaces.Services;

public interface INiaClient
{
    Task<(bool IsValid, string PhotoUrl)> VerifyGhanaCardAsync(string idNumber, string firstName, string lastName, DateOnly dateOfBirth);
}
