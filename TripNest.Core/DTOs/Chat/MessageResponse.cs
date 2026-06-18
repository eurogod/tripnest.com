using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Chat;

public class MessageResponse
{
    public required string MessageId { get; set; }
    public required string ConversationId { get; set; }
    public required string SenderId { get; set; }
    public required string Content { get; set; }
    public MessageType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}
