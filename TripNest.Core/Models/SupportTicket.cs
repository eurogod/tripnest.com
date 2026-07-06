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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedById { get; set; }
}
