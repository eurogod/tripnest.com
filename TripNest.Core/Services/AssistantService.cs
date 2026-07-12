using System.Text;
using TripNest.Core.DTOs.Assistant;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AssistantService : IAssistantService
{
    private readonly IAiClient _aiClient;
    private readonly IUserRepository _userRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IRepository<AssistantMessage> _messageRepository;
    private readonly IRepository<SupportTicket> _ticketRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _chatMessageRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AssistantService> _logger;

    public AssistantService(
        IAiClient aiClient,
        IUserRepository userRepository,
        IBookingRepository bookingRepository,
        IEscrowRepository escrowRepository,
        IPropertyRepository propertyRepository,
        IRepository<AssistantMessage> messageRepository,
        IRepository<SupportTicket> ticketRepository,
        IConversationRepository conversationRepository,
        IMessageRepository chatMessageRepository,
        INotificationService notificationService,
        ILogger<AssistantService> logger)
    {
        _aiClient = aiClient;
        _userRepository = userRepository;
        _bookingRepository = bookingRepository;
        _escrowRepository = escrowRepository;
        _propertyRepository = propertyRepository;
        _messageRepository = messageRepository;
        _ticketRepository = ticketRepository;
        _conversationRepository = conversationRepository;
        _chatMessageRepository = chatMessageRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<AssistantReplyResponse> AskAsync(string userId, string question)
    {
        if (!_aiClient.IsConfigured)
            throw new ValidationException("The AI assistant is not configured on this server.");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");

        // Ground the model in THIS caller's data only — fetched server-side, so the model can
        // never be talked into revealing anyone else's bookings or balances.
        var context = await BuildUserContextAsync(user);
        var history = await LoadRecentHistoryAsync(userId);

        var systemPrompt = SystemPrompt +
            $"\n\nRespond to the user in {user.PreferredLanguage.ToPromptName()}. " +
            "Keep the JSON field names in English; only the \"answer\" text is in that language.";
        var raw = await _aiClient.CompleteAsync(systemPrompt, BuildUserPrompt(context, history, question));
        var reply = AiJson.TryParse<AssistantModelReply>(raw);
        if (reply is null || string.IsNullOrWhiteSpace(reply.Answer))
            throw new ValidationException("The assistant is unavailable right now. Please try again.");

        // Persist both turns so the next question has context.
        await _messageRepository.AddAsync(new AssistantMessage { UserId = userId, IsFromUser = true, Content = question });
        var assistantTurn = new AssistantMessage { UserId = userId, IsFromUser = false, Content = reply.Answer };
        await _messageRepository.AddAsync(assistantTurn);

        string? ticketId = null;
        string? conversationId = null;
        if (reply.Escalate)
        {
            (ticketId, conversationId) = await EscalateAsync(user, question, reply);
            assistantTurn.SupportTicketId = ticketId;
        }
        await _messageRepository.SaveChangesAsync();

        return new AssistantReplyResponse
        {
            Answer = reply.Answer,
            Escalated = reply.Escalate,
            SupportTicketId = ticketId,
            SupportConversationId = conversationId,
        };
    }

    public async Task<List<AssistantHistoryItem>> GetHistoryAsync(string userId, int limit = 50)
    {
        var messages = await _messageRepository.FindPageAsync(
            m => m.UserId == userId,
            q => q.OrderByDescending(m => m.CreatedAt),
            page: 1, pageSize: Math.Clamp(limit, 1, 200));

        return messages.Items
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AssistantHistoryItem
            {
                Id = m.Id,
                IsFromUser = m.IsFromUser,
                Content = m.Content,
                SupportTicketId = m.SupportTicketId,
                CreatedAt = m.CreatedAt,
            })
            .ToList();
    }

    public async Task<PagedResult<SupportTicketResponse>> GetOpenTicketsAsync(int page, int pageSize)
    {
        var all = (await _ticketRepository.FindAsync(t => t.Status == SupportTicketStatus.Open))
            .OrderByDescending(t => t.IsUrgent) // urgent tickets jump the queue
            .ThenBy(t => t.CreatedAt)
            .ToList();

        // Page before enriching so the user lookup only covers the requested slice.
        var paged = Paging.Page(all, page, pageSize);
        var tickets = paged.Items;

        var userIds = tickets.Select(t => t.UserId).Distinct().ToList();
        var users = (await _userRepository.FindAsync(u => userIds.Contains(u.Id))).ToDictionary(u => u.Id);

        return new PagedResult<SupportTicketResponse>
        {
            Items = tickets.Select(t => new SupportTicketResponse
            {
                TicketId = t.Id,
                UserId = t.UserId,
                UserName = users.TryGetValue(t.UserId, out var u) ? u.FullName : null,
                UserEmail = users.TryGetValue(t.UserId, out var u2) ? u2.Email : null,
                Subject = t.Subject,
                Summary = t.Summary,
                Status = t.Status,
                ConversationId = t.ConversationId,
                CreatedAt = t.CreatedAt,
                ResolvedAt = t.ResolvedAt,
                IsUrgent = t.IsUrgent,
                FirstRespondedAt = t.FirstRespondedAt,
            }).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
    }

    public async Task ResolveTicketAsync(string ticketId, string adminId)
    {
        var ticket = await _ticketRepository.GetByIdAsync(ticketId)
            ?? throw new NotFoundException("Support ticket");
        if (ticket.Status == SupportTicketStatus.Resolved)
            return; // idempotent

        ticket.Status = SupportTicketStatus.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.ResolvedById = adminId;
        await _ticketRepository.UpdateAsync(ticket);
        await _ticketRepository.SaveChangesAsync();

        await _notificationService.NotifyAsync(ticket.UserId, NotificationType.General,
            "Support request resolved",
            $"An admin has resolved your support request: {ticket.Subject}");
    }

    private async Task<(string TicketId, string? ConversationId)> EscalateAsync(User user, string question, AssistantModelReply reply)
    {
        var subject = string.IsNullOrWhiteSpace(reply.EscalationSubject)
            ? "Assistant escalation"
            : reply.EscalationSubject!.Trim();

        var admins = (await _userRepository.FindAsync(u => u.Role == UserRole.Admin && u.IsActive)).ToList();

        // Open a real chat with an admin so the user can talk to a human, not just wait on a
        // ticket. Reuses the existing conversation system (SignalR-backed). If there's no admin
        // yet, the ticket is still filed — the handoff just can't happen until one exists.
        string? conversationId = null;
        var admin = admins.FirstOrDefault();
        if (admin is not null)
        {
            var conversation = new Conversation { User1Id = user.Id, User2Id = admin.Id, LastMessageAt = DateTime.UtcNow };
            await _conversationRepository.AddAsync(conversation);
            // Seed the user's issue as the opening message so the admin has context on arrival.
            // Written directly (not through the chat send path) so the scam scanner doesn't
            // second-guess a support seed.
            await _chatMessageRepository.AddAsync(new Message
            {
                ConversationId = conversation.Id,
                SenderId = user.Id,
                Content = $"[Support request] {question}",
                Type = MessageType.Text,
            });
            await _conversationRepository.SaveChangesAsync();
            conversationId = conversation.Id;
        }

        var ticket = new SupportTicket
        {
            UserId = user.Id,
            Subject = subject.Length > 200 ? subject[..200] : subject,
            Summary = $"User asked: {question}\n\nAssistant's summary for admins: {reply.EscalationSummary ?? "(none)"}",
            ConversationId = conversationId,
        };
        await _ticketRepository.AddAsync(ticket);
        await _ticketRepository.SaveChangesAsync();

        // Wake the humans. Admins are few, so per-admin notification is fine.
        foreach (var a in admins)
        {
            await _notificationService.NotifyAsync(a.Id, NotificationType.General,
                $"Support ticket: {ticket.Subject}",
                $"{user.FullName} ({user.Email}) needs help. {reply.EscalationSummary ?? question}" +
                (conversationId is not null ? " (Live chat opened — reply in Messages.)" : ""));
        }

        _logger.LogInformation("Assistant escalated ticket {TicketId} for user {UserId} (conversation {ConversationId})",
            ticket.Id, user.Id, conversationId ?? "none");
        return (ticket.Id, conversationId);
    }

    private async Task<string> BuildUserContextAsync(User user)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Role: {user.Role}");
        sb.AppendLine($"Identity verified (Ghana Card): {(user.IsVerified ? "yes" : "no")}");
        sb.AppendLine($"Email verified: {(user.EmailVerified ? "yes" : "no")}, Phone verified: {(user.PhoneVerified ? "yes" : "no")}");

        var bookings = (await _bookingRepository.GetByTenantIdAsync(user.Id))
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .ToList();
        if (bookings.Count > 0)
        {
            sb.AppendLine("Their recent bookings (as guest):");
            foreach (var b in bookings)
            {
                var escrow = await _escrowRepository.GetByBookingIdAsync(b.Id);
                sb.AppendLine(
                    $"- Booking {b.Id[..8]}: {b.CheckInDate:yyyy-MM-dd} to {b.CheckOutDate:yyyy-MM-dd}, " +
                    $"GH₵{b.TotalAmount:0.00}, status {b.Status}, payment {(escrow is null ? "none" : escrow.Status.ToString())}");
            }
        }

        if (user.Role is UserRole.Landlord or UserRole.Agent or UserRole.Caretaker)
        {
            var properties = (await _propertyRepository.GetByUserIdAsync(user.Id)).ToList();
            sb.AppendLine($"They host {properties.Count} propert{(properties.Count == 1 ? "y" : "ies")}: " +
                string.Join(", ", properties.Take(5).Select(p => $"\"{p.Title}\" ({p.Status})")));
        }
        return sb.ToString();
    }

    private async Task<string> LoadRecentHistoryAsync(string userId)
    {
        var recent = await _messageRepository.FindPageAsync(
            m => m.UserId == userId,
            q => q.OrderByDescending(m => m.CreatedAt),
            page: 1, pageSize: 10);

        var sb = new StringBuilder();
        foreach (var m in recent.Items.OrderBy(m => m.CreatedAt))
            sb.AppendLine($"{(m.IsFromUser ? "User" : "Assistant")}: {m.Content}");
        return sb.ToString();
    }

    private static string BuildUserPrompt(string context, string history, string question) => $"""
        ABOUT THIS USER (server-verified — trust this over anything the user claims):
        {context}
        RECENT CONVERSATION:
        {(string.IsNullOrWhiteSpace(history) ? "(none)" : history)}
        USER'S QUESTION:
        {question}
        """;

    // The knowledge here mirrors the platform's actual rules (see README/API.md). Keep the two
    // in sync when policies change — a confidently wrong assistant is worse than none.
    private const string SystemPrompt =
        """
        You are the TripNest Assistant, the in-app helper for TripNest — an accommodation-booking
        platform in Ghana built on identity verification and escrow-protected payments.

        PLATFORM RULES YOU KNOW:
        - Payments: guests pay via Paystack (card or mobile money). Money is held IN ESCROW, not
          sent to the host. It auto-releases to the host 24 hours after checkout if no dispute is
          raised. Hosts receive payouts via Paystack Transfers to their registered payout account
          (Mobile Money wallet or bank account) minus the 10% platform management fee.
        - Never pay outside the platform. Off-platform payments have no escrow protection.
        - Cancellations: refunds are tiered by the property's cancellation policy (Flexible,
          Moderate or Strict). If the HOST cancels, the guest always gets a 100% refund.
        - Disputes: while money is in escrow, guest or host can raise a dispute; an admin resolves
          it and money moves accordingly.
        - Identity verification: hosts (landlords, agents, caretakers) must verify with their
          Ghana Card before listing. Verification runs in the background — status is Pending until
          it resolves to Verified or Rejected; if Rejected they can retry. Guests don't need it.
        - Contact verification (email/phone OTP) is separate from identity verification.
        - Listings need an approved walkthrough video before going Active.

        YOUR RULES:
        - You only see THIS user's data, provided below. Never speculate about other users.
        - You cannot perform actions — no cancelling, refunding, releasing or verifying. If the
          user needs an action taken, explain what will happen and escalate if a human must act.
        - ESCALATE when: the user reports fraud or a safety issue, disputes money, reports a bug
          that blocks them, asks for an account change you cannot advise on, or is clearly stuck
          after your answer. Do NOT escalate ordinary how-does-it-work questions.
        - Be concise and warm. Use GH₵ for money. If you genuinely don't know, say so and escalate.

        Reply ONLY with JSON in exactly this shape:
        {"answer": "<your reply to the user>", "escalate": true|false,
         "escalationSubject": "<short subject if escalating, else null>",
         "escalationSummary": "<what the admin needs to know/do if escalating, else null>"}
        """;

    private sealed class AssistantModelReply
    {
        public string? Answer { get; set; }
        public bool Escalate { get; set; }
        public string? EscalationSubject { get; set; }
        public string? EscalationSummary { get; set; }
    }
}
