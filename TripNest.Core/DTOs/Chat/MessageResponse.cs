using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Chat;

public class MessageResponse
{
    public required string MessageId { get; set; }
    public required string ConversationId { get; set; }
    public required string SenderId { get; set; }
    public required string Content { get; set; }
    public MessageType Type { get; set; }
    /// <summary>Servable URL/path of the attachment (null for plain text).</summary>
    public string? MediaUrl { get; set; }
    public string? MediaType { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
