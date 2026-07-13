using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string User1Id { get; set; }
    public User? User1 { get; set; }
    public required string User2Id { get; set; }
    public User? User2 { get; set; }
    public string? PropertyId { get; set; }
    public Property? Property { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}

public class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public required string SenderId { get; set; }
    public User? Sender { get; set; }
    public required string Content { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;
    /// <summary>Stored path of an attached image/voice-note/document (null for text messages).</summary>
    public string? MediaPath { get; set; }
    /// <summary>MIME type of the attachment, for the client to render/play/download it.</summary>
    public string? MediaType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
}
