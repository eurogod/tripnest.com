namespace TripNest.Core.DTOs.Chat;

public class StartConversationRequest
{
    public required string OtherUserId { get; set; }
    public string? PropertyId { get; set; }
}

public class SendMessageRequest
{
    public required string Body { get; set; }
}
