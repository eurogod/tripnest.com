namespace TripNest.Core.Security;

/// <summary>
/// Central password-strength policy applied on registration, change, and reset.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 10;

    // A small deny-list of the most common/breached passwords that still satisfy the
    // length + letter + digit rules (so the structural checks alone wouldn't catch them).
    // Compared case-insensitively against the whole password. This is a cheap, offline guard;
    // for stronger coverage screen against a breached-password corpus (e.g. HaveIBeenPwned).
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password1", "password12", "password123", "password1234",
        "qwerty123", "qwerty1234", "abc123456", "123456789", "1234567890",
        "letmein123", "welcome123", "admin12345", "iloveyou123", "passw0rd123",
        "monkey1234", "football12", "baseball12", "dragon1234", "sunshine12",
    };

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> if the password does not meet the policy:
    /// at least <see cref="MinLength"/> characters, with a letter and a digit, and not a
    /// well-known/common password.
    /// </summary>
    public static void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            throw new InvalidOperationException($"Password must be at least {MinLength} characters long");

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new InvalidOperationException("Password must contain at least one letter and one digit");

        if (CommonPasswords.Contains(password))
            throw new InvalidOperationException("This password is too common. Please choose a stronger one.");
    }
}
