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
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        ILogger<ChatService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<List<ConversationResponse>> GetUserConversationsAsync(string userId)
    {
        try
        {
            var conversations = await _conversationRepository.GetUserConversationsAsync(userId);
            return conversations.Select(MapConversation).ToList();
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

            return MapConversation(conversation);
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

            await _messageRepository.AddAsync(message);
            await _messageRepository.SaveChangesAsync();

            conversation.LastMessageAt = DateTime.UtcNow;
            await _conversationRepository.UpdateAsync(conversation);
            await _conversationRepository.SaveChangesAsync();

            _logger.LogInformation("Message sent: {MessageId} in conversation {ConversationId} by {UserId}",
                message.Id, conversationId, userId);

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
}
