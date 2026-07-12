using System.ComponentModel.DataAnnotations;
using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Assistant;

public class AskAssistantRequest
{
    [StringLength(2000, MinimumLength = 2)]
    public required string Question { get; set; }
}

public class AssistantReplyResponse
{
    public required string Answer { get; set; }
    public bool Escalated { get; set; }
    /// <summary>Set when the question was escalated to a human — the ticket admins will see.</summary>
    public string? SupportTicketId { get; set; }
    /// <summary>Set when escalation opened a live chat with support — navigate the user here to talk to an admin.</summary>
    public string? SupportConversationId { get; set; }
}

public class AssistantHistoryItem
{
    public required string Id { get; set; }
    public bool IsFromUser { get; set; }
    public required string Content { get; set; }
    public string? SupportTicketId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SupportTicketResponse
{
    public required string TicketId { get; set; }
    public required string UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public required string Subject { get; set; }
    public required string Summary { get; set; }
    public SupportTicketStatus Status { get; set; }
    public string? ConversationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsUrgent { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
}
