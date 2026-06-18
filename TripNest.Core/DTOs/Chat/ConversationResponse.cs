namespace TripNest.Core.DTOs.Chat;

public class ConversationResponse
{
    public required string ConversationId { get; set; }
    public required string User1Id { get; set; }
    public required string User2Id { get; set; }
    public string? PropertyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}
