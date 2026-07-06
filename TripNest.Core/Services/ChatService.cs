using TripNest.Core.DTOs.Chat;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class ChatService : IChatService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IAiClient _aiClient;
    private readonly IScamDetectionService _scamDetection;
    private readonly Hubs.IPresenceTracker _presence;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IRepository<User> userRepository,
        IPropertyRepository propertyRepository,
        IAiClient aiClient,
        IScamDetectionService scamDetection,
        Hubs.IPresenceTracker presence,
        ILogger<ChatService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _propertyRepository = propertyRepository;
        _aiClient = aiClient;
        _scamDetection = scamDetection;
        _presence = presence;
        _logger = logger;
    }

    public async Task<List<ConversationResponse>> GetUserConversationsAsync(string userId)
    {
        try
        {
            var conversations = (await _conversationRepository.GetUserConversationsAsync(userId)).ToList();
            if (conversations.Count == 0)
                return new List<ConversationResponse>();

            // Enrich the list view in three set-based queries (names, previews,
            // unread counts) so the client never needs a per-row lookup.
            var otherIds = conversations
                .Select(c => c.User1Id == userId ? c.User2Id : c.User1Id)
                .Distinct()
                .ToList();
            var otherUsers = (await _userRepository.FindAsync(u => otherIds.Contains(u.Id)))
                .ToDictionary(u => u.Id);

            var conversationIds = conversations.Select(c => c.Id).ToList();
            var messages = (await _messageRepository.FindAsync(m => conversationIds.Contains(m.ConversationId))).ToList();
            var lastMessageByConversation = messages
                .GroupBy(m => m.ConversationId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedAt).First().Content);
            var unreadByConversation = messages
                .Where(m => !m.IsRead && m.SenderId != userId)
                .GroupBy(m => m.ConversationId)
                .ToDictionary(g => g.Key, g => g.Count());

            return conversations.Select(c =>
            {
                var otherId = c.User1Id == userId ? c.User2Id : c.User1Id;
                var otherUser = otherUsers.GetValueOrDefault(otherId);
                var response = MapConversation(c);
                response.OtherUserId = otherId;
                response.OtherUserName = otherUser?.FullName;
                response.LastMessagePreview = lastMessageByConversation.GetValueOrDefault(c.Id);
                response.UnreadCount = unreadByConversation.GetValueOrDefault(c.Id);
                response.OtherUserIsOnline = _presence.IsOnline(otherId);
                response.OtherUserLastSeenAt = response.OtherUserIsOnline ? null : otherUser?.LastSeenAt;
                return response;
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ConversationResponse> StartConversationAsync(string userId, string otherUserId, string? propertyId)
    {
        try
        {
            var existing = await _conversationRepository.GetConversationAsync(userId, otherUserId);
            if (existing != null)
                return MapConversation(existing);

            var conversation = new Conversation
            {
                User1Id = userId,
                User2Id = otherUserId,
                PropertyId = propertyId
            };

            await _conversationRepository.AddAsync(conversation);
            await _conversationRepository.SaveChangesAsync();

            _logger.LogInformation("Conversation started: {ConversationId} between {UserId} and {OtherUserId}",
                conversation.Id, userId, otherUserId);

            return MapConversation(conversation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation between {UserId} and {OtherUserId}", userId, otherUserId);
            throw;
        }
    }

    public async Task<ConversationResponse?> GetConversationAsync(string conversationId, string userId)
    {
        try
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null)
                return null;

            if (conversation.User1Id != userId && conversation.User2Id != userId)
                return null;

            var otherId = conversation.User1Id == userId ? conversation.User2Id : conversation.User1Id;
            var otherUser = await _userRepository.GetByIdAsync(otherId);

            var response = MapConversation(conversation);
            response.OtherUserId = otherId;
            response.OtherUserName = otherUser?.FullName;
            response.OtherUserIsOnline = _presence.IsOnline(otherId);
            response.OtherUserLastSeenAt = response.OtherUserIsOnline ? null : otherUser?.LastSeenAt;
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation {ConversationId} for user {UserId}", conversationId, userId);
            throw;
        }
    }

    public async Task<PagedResult<MessageResponse>> GetConversationMessagesAsync(
        string conversationId, string userId, int page, int pageSize)
    {
        try
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null || (conversation.User1Id != userId && conversation.User2Id != userId))
            {
                return new PagedResult<MessageResponse>
                {
                    Items = Enumerable.Empty<MessageResponse>(),
                    TotalCount = 0,
                    Page = page,
                    PageSize = pageSize
                };
            }

            var allMessages = await _messageRepository.GetByConversationIdAsync(conversationId);
            var ordered = allMessages.OrderByDescending(m => m.CreatedAt).ToList();

            var totalCount = ordered.Count;
            var items = ordered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(MapMessage)
                .ToList();

            return new PagedResult<MessageResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for conversation {ConversationId}", conversationId);
            throw;
        }
    }

    public async Task<MessageResponse> SendMessageAsync(string conversationId, string userId, string body)
    {
        try
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null)
                throw new InvalidOperationException("Conversation not found");

            if (conversation.User1Id != userId && conversation.User2Id != userId)
                throw new UnauthorizedAccessException("User is not a participant in this conversation");

            var message = new Message
            {
                ConversationId = conversationId,
                SenderId = userId,
                Content = body,
                Type = MessageType.Text
            };

            // One commit for the message + conversation stamp (shared DbContext), so a crash
            // between them can't leave a message the conversation list doesn't know about.
            await _messageRepository.AddAsync(message);
            conversation.LastMessageAt = DateTime.UtcNow;
            await _conversationRepository.UpdateAsync(conversation);
            await _messageRepository.SaveChangesAsync();

            _logger.LogInformation("Message sent: {MessageId} in conversation {ConversationId} by {UserId}",
                message.Id, conversationId, userId);

            // Trust layer: watch for off-platform payment attempts. Swallows its own errors and
            // only calls the AI on rule hits, so ordinary messages pay zero latency for it.
            await _scamDetection.ScanMessageAsync(message, conversation);

            return MapMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message in conversation {ConversationId} by user {UserId}", conversationId, userId);
            throw;
        }
    }

    public async Task MarkMessageAsReadAsync(string messageId, string userId)
    {
        try
        {
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message == null)
                throw new InvalidOperationException("Message not found");

            // Only a participant of the conversation may mark its messages read — without this,
            // any authenticated user holding a message id could tamper with read receipts.
            var conversation = await _conversationRepository.GetByIdAsync(message.ConversationId);
            if (conversation == null || (conversation.User1Id != userId && conversation.User2Id != userId))
                throw new UnauthorizedAccessException("User is not a participant in this conversation");

            if (message.SenderId == userId || message.IsRead)
                return;

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;

            await _messageRepository.UpdateAsync(message);
            await _messageRepository.SaveChangesAsync();

            _logger.LogInformation("Message {MessageId} marked as read by {UserId}", messageId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message {MessageId} as read for user {UserId}", messageId, userId);
            throw;
        }
    }

    public async Task MarkConversationAsReadAsync(string conversationId, string userId)
    {
        try
        {
            var messages = await _messageRepository.GetByConversationIdAsync(conversationId);
            var unread = messages.Where(m => m.SenderId != userId && !m.IsRead).ToList();

            foreach (var message in unread)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
                await _messageRepository.UpdateAsync(message);
            }

            if (unread.Count > 0)
            {
                await _messageRepository.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} messages as read in conversation {ConversationId} for user {UserId}",
                    unread.Count, conversationId, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation {ConversationId} as read for user {UserId}", conversationId, userId);
            throw;
        }
    }

    public async Task DeleteConversationAsync(string conversationId, string userId)
    {
        try
        {
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null)
                throw new InvalidOperationException("Conversation not found");

            if (conversation.User1Id != userId && conversation.User2Id != userId)
                throw new UnauthorizedAccessException("User is not a participant in this conversation");

            var messages = await _messageRepository.GetByConversationIdAsync(conversationId);
            foreach (var message in messages)
                await _messageRepository.DeleteAsync(message);

            if (messages.Any())
                await _messageRepository.SaveChangesAsync();

            await _conversationRepository.DeleteAsync(conversation);
            await _conversationRepository.SaveChangesAsync();

            _logger.LogInformation("Conversation {ConversationId} deleted by user {UserId}", conversationId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation {ConversationId} for user {UserId}", conversationId, userId);
            throw;
        }
    }

    private static ConversationResponse MapConversation(Conversation c) =>
        new ConversationResponse
        {
            ConversationId = c.Id,
            User1Id = c.User1Id,
            User2Id = c.User2Id,
            PropertyId = c.PropertyId,
            CreatedAt = c.CreatedAt,
            LastMessageAt = c.LastMessageAt
        };

    private static MessageResponse MapMessage(Message m) =>
        new MessageResponse
        {
            MessageId = m.Id,
            ConversationId = m.ConversationId,
            SenderId = m.SenderId,
            Content = m.Content,
            Type = m.Type,
            CreatedAt = m.CreatedAt,
            IsRead = m.IsRead,
            ReadAt = m.ReadAt
        };

    /// <summary>
    /// Drafts a reply the participant can edit and send — grounded in the linked listing's
    /// facts so the suggestion can answer questions like "is there parking?" truthfully.
    /// </summary>
    public async Task<string> SuggestReplyAsync(string conversationId, string userId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        if (conversation == null)
            throw new InvalidOperationException("Conversation not found");
        if (conversation.User1Id != userId && conversation.User2Id != userId)
            throw new UnauthorizedAccessException("User is not a participant in this conversation");

        if (!_aiClient.IsConfigured)
            throw new InvalidOperationException("AI suggestions are not configured on this server.");

        var messages = (await _messageRepository.GetByConversationIdAsync(conversationId))
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .ToList();
        if (messages.Count == 0)
            throw new InvalidOperationException("There are no messages to reply to yet.");

        var property = conversation.PropertyId is not null
            ? await _propertyRepository.GetByIdAsync(conversation.PropertyId)
            : null;
        var isHost = property is not null && property.UserId == userId;

        var facts = property is null
            ? "(no listing linked to this conversation)"
            : ListingCopyPrompts.Facts(property);
        var transcript = string.Join("\n", messages.Select(m =>
            (m.SenderId == userId ? "You: " : "Them: ") + m.Content));

        var systemPrompt =
            "You draft chat replies for a user on TripNest, an accommodation-booking platform in Ghana. " +
            (isHost
                ? "The user is the HOST of the listing below. "
                : "The user is a guest interested in the listing below. ") +
            "Draft ONE short, friendly reply (1-3 sentences) to the latest message from the other person. " +
            "Only state facts about the listing that appear below - if the answer is not in the facts, " +
            "say you will check and confirm. Never suggest paying or communicating outside the platform. " +
            "Reply ONLY with JSON: {\"reply\": \"<the suggested reply>\"}";

        var raw = await _aiClient.CompleteAsync(systemPrompt,
            "LISTING FACTS:\n" + facts + "\n\nCONVERSATION (most recent last):\n" + transcript);

        var suggestion = AiJson.TryParse<SuggestedReply>(raw);
        if (suggestion is null || string.IsNullOrWhiteSpace(suggestion.Reply))
            throw new InvalidOperationException("Suggestions are unavailable right now. Please try again.");
        return suggestion.Reply;
    }

    private sealed class SuggestedReply
    {
        public string? Reply { get; set; }
    }
}
