namespace TripNest.Core.Interfaces.Services;

public interface IPhoneNumberValidator
{
    /// <summary>True if the value parses to a valid phone number for the default region (Ghana).</summary>
    bool IsValid(string? phone);

    /// <summary>Returns the number in E.164 form (e.g. +233241234567), or null if it isn't valid.</summary>
    string? Normalize(string? phone);
}
