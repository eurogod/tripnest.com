using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// A support request escalated to admins — usually by the AI assistant when a question needs a
/// human (disputes, account problems, anything touching money decisions). The assistant only
/// files these; resolving them is always a human action.
/// </summary>
public class SupportTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string Subject { get; set; }
    /// <summary>The user's original question plus the assistant's summary of what the admin needs to do.</summary>
    public required string Summary { get; set; }
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    /// <summary>The live chat opened between the user and an admin for this ticket (if an admin exists).</summary>
    public string? ConversationId { get; set; }
    /// <summary>Urgent tickets (lockout / unsafe guest) jump the queue and page every admin
    /// through the emergency channel (opt-outs bypassed). The 15-minute human-response promise.</summary>
    public bool IsUrgent { get; set; }
    /// <summary>When an admin first acknowledged the ticket — the SLA clock's stop line.</summary>
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedById { get; set; }
}
