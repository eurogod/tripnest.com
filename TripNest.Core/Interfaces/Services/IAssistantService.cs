using TripNest.Core.DTOs.Assistant;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Interfaces.Services;

/// <summary>
/// The TripNest Assistant: answers platform questions grounded in the caller's OWN data
/// (bookings, escrow, verification) and escalates to a human admin via a support ticket when it
/// can't help. Strictly advisory — it never mutates state beyond its own history and tickets.
/// </summary>
public interface IAssistantService
{
    Task<AssistantReplyResponse> AskAsync(string userId, string question);
    /// <summary>Direct customer-care handoff (no AI): files a ticket + opens an admin chat, so
    /// reaching a human never depends on the assistant being configured or available.</summary>
    Task<AssistantReplyResponse> ContactSupportAsync(string userId, string? message);
    Task<List<AssistantHistoryItem>> GetHistoryAsync(string userId, int limit = 50);
    Task<PagedResult<SupportTicketResponse>> GetOpenTicketsAsync(int page, int pageSize);
    Task ResolveTicketAsync(string ticketId, string adminId);
}
