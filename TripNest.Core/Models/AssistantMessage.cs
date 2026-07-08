namespace TripNest.Core.Models;

/// <summary>
/// One turn of a user's conversation with the AI assistant. Persisted so the assistant keeps
/// context across questions and the user can review what it told them.
/// </summary>
public class AssistantMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public bool IsFromUser { get; set; }
    public required string Content { get; set; }
    /// <summary>Set when this assistant turn escalated to a support ticket.</summary>
    public string? SupportTicketId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
