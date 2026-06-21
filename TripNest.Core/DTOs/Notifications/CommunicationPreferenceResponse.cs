namespace TripNest.Core.DTOs.Notifications;

public class CommunicationPreferenceResponse
{
    public required string UserId { get; set; }
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
}

public class UpdateCommunicationPreferenceRequest
{
    public bool SmsEnabled { get; set; }
    public bool EmailEnabled { get; set; }
}
