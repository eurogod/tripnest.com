namespace TripNest.Core.DTOs.Auth;

/// <summary>Requests a one-time login code for the phone number, if it belongs to an account.</summary>
public class PhoneLoginStartRequest
{
    public required string Phone { get; set; }
}

/// <summary>Exchanges the SMS code sent to the phone number for a normal login session.</summary>
public class PhoneLoginVerifyRequest
{
    public required string Phone { get; set; }

    public required string Code { get; set; }
}
