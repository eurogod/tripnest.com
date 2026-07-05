using System.ComponentModel.DataAnnotations;

namespace TripNest.Core.DTOs.Chat;

public class StartConversationRequest
{
    public required string OtherUserId { get; set; }
    public string? PropertyId { get; set; }
}

public class SendMessageRequest
{
    [StringLength(4000, MinimumLength = 1)]
    public required string Body { get; set; }
}
