namespace TripNest.Core.DTOs.Chat;

public class ConversationResponse
{
    public required string ConversationId { get; set; }
    public required string User1Id { get; set; }
    public required string User2Id { get; set; }
    public string? PropertyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }

    // List-view enrichment (populated for the caller's conversation list so the
    // client can render name/preview/badge without an extra request per row).
    public string? OtherUserId { get; set; }
    public string? OtherUserName { get; set; }
    public string? LastMessagePreview { get; set; }
    public int UnreadCount { get; set; }

    // Presence of the other participant: online now, or the last time they were seen (day + time).
    // For an offline user the client renders "last seen {OtherUserLastSeenAt}".
    public bool OtherUserIsOnline { get; set; }
    public DateTime? OtherUserLastSeenAt { get; set; }
}
