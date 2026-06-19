namespace TripNest.Core.Security;

/// <summary>
/// Central password-strength policy applied on registration, change, and reset.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the password does not meet the policy:
    /// at least <see cref="MinLength"/> characters, with a letter and a digit.
    /// </summary>
    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new InvalidOperationException($"Password must be at least {MinLength} characters long");

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new InvalidOperationException("Password must contain at least one letter and one digit");
    }
}
