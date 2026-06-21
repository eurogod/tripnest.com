namespace TripNest.Core.DTOs.Safety;

/// <summary>The trusted contact saved on the user's profile, reused by safe-arrival check-ins.</summary>
public class TrustedContactRequest
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class TrustedContactResponse
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
