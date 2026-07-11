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
    Task<List<AssistantHistoryItem>> GetHistoryAsync(string userId, int limit = 50);
    Task<PagedResult<SupportTicketResponse>> GetOpenTicketsAsync(int page, int pageSize);
    Task ResolveTicketAsync(string ticketId, string adminId);
}
