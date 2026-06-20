namespace TripNest.Core.DTOs.Auth;

public class SendOtpRequest
{
    /// <summary>"sms" (default) or "whatsapp".</summary>
    public string Channel { get; set; } = "sms";
}

public class VerifyOtpRequest
{
    public required string Code { get; set; }
}
