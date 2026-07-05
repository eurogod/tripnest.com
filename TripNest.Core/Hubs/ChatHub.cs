using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Extensions;
using TripNest.Core.DTOs.Chat;

namespace TripNest.Core.Hubs;

/// <summary>
/// SignalR hub for real-time chat messaging.
/// Clients connect per conversation and receive live messages.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPresenceTracker _presence;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(
        IChatService chatService,
        IMessageRepository messageRepository,
        IConversationRepository conversationRepository,
        IUserRepository userRepository,
        IPresenceTracker presence,
        ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _messageRepository = messageRepository;
        _conversationRepository = conversationRepository;
        _userRepository = userRepository;
        _presence = presence;
        _logger = logger;
    }

    /// <summary>
    /// Marks the connecting user online and, if they just came online, tells their conversation
    /// partners so their UI can flip the presence indicator without polling.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User.GetUserId();
        if (!string.IsNullOrEmpty(userId) && _presence.Connect(userId, Context.ConnectionId))
            await NotifyPartnersOfPresenceAsync(userId, isOnline: true, lastSeenAt: null);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Sends the caller the current presence (online / last-seen) of a specific user. Presence is
    /// only visible to the user themself and their conversation partners — the same audience the
    /// PresenceChanged push goes to — so arbitrary users can't be watched.
    /// </summary>
    public async Task<object> GetPresence(string userId)
    {
        var callerId = Context.User.GetUserId();
        if (string.IsNullOrEmpty(callerId))
            throw new HubException("Unauthorized");

        if (callerId != userId)
        {
            var conversations = await _conversationRepository.GetUserConversationsAsync(callerId);
            var isPartner = conversations.Any(c => c.User1Id == userId || c.User2Id == userId);
            if (!isPartner)
                throw new HubException("You can only view presence for your conversation partners.");
        }

        if (_presence.IsOnline(userId))
            return new { userId, isOnline = true, lastSeenAt = (DateTime?)null };

        var user = await _userRepository.GetByIdAsync(userId);
        return new { userId, isOnline = false, lastSeenAt = user?.LastSeenAt };
    }

    private async Task NotifyPartnersOfPresenceAsync(string userId, bool isOnline, DateTime? lastSeenAt)
    {
        var conversations = await _conversationRepository.GetUserConversationsAsync(userId);
        var partnerIds = conversations
            .Select(c => c.User1Id == userId ? c.User2Id : c.User1Id)
            .Distinct()
            .ToList();
        if (partnerIds.Count > 0)
            await Clients.Users(partnerIds).SendAsync("PresenceChanged", new { userId, isOnline, lastSeenAt });
    }

    /// <summary>
    /// Joins user to a conversation group for real-time updates.
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        try
        {
            var userId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            // Verify user is part of this conversation
            var conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation == null ||
                (conversation.User1Id != userId && conversation.User2Id != userId))
            {
                await Clients.Caller.SendAsync("Error", "Access denied to this conversation");
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
            _logger.LogInformation("User {UserId} joined conversation {ConversationId}", userId, conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining conversation");
            await Clients.Caller.SendAsync("Error", "Failed to join conversation");
        }
    }

    /// <summary>
    /// Sends a message in a conversation.
    /// Message is persisted and broadcast to conversation participants.
    /// </summary>
    public async Task SendMessage(string conversationId, string body)
    {
        try
        {
            var userId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(body?.Trim()))
            {
                await Clients.Caller.SendAsync("Error", "Invalid message");
                return;
            }

            // One code path with the REST endpoint: the service validates participation, persists
            // the message and stamps the conversation; the hub only broadcasts the same payload.
            var message = await _chatService.SendMessageAsync(conversationId, userId, body.Trim());
            await Clients.Group(conversationId).SendAsync("ReceiveMessage", message);

            _logger.LogInformation("Message sent in conversation {ConversationId} by user {UserId}",
                conversationId, userId);
        }
        catch (UnauthorizedAccessException)
        {
            await Clients.Caller.SendAsync("Error", "Access denied to this conversation");
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("Error", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            await Clients.Caller.SendAsync("Error", "Failed to send message");
        }
    }

    /// <summary>
    /// Marks a message as read.
    /// </summary>
    public async Task MarkMessageAsRead(string messageId)
    {
        try
        {
            var userId = Context.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", "Unauthorized");
                return;
            }

            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message == null)
            {
                await Clients.Caller.SendAsync("Error", "Message not found");
                return;
            }

            // Same validation + persistence path as the REST endpoint (participant check lives in
            // the service). The service no-ops for the sender or an already-read message; only a
            // genuine transition broadcasts, matching the previous behaviour.
            var wasAlreadyRead = message.IsRead;
            await _chatService.MarkMessageAsReadAsync(messageId, userId);

            if (!wasAlreadyRead && message.IsRead)
            {
                await Clients.Group(message.ConversationId).SendAsync("MessageRead", new
                {
                    messageId,
                    readAt = message.ReadAt
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            await Clients.Caller.SendAsync("Error", "Access denied");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read");
            await Clients.Caller.SendAsync("Error", "Failed to mark message as read");
        }
    }

    /// <summary>
    /// Signals that the caller has started typing in a conversation.
    /// Broadcast to the other participant(s) only — never echoed to the sender.
    /// </summary>
    public async Task Typing(string conversationId)
    {
        var userId = Context.User.GetUserId();
        if (string.IsNullOrEmpty(userId) || !await IsParticipantAsync(conversationId, userId))
            return;

        await Clients.OthersInGroup(conversationId).SendAsync("UserTyping", new { conversationId, userId });
    }

    /// <summary>
    /// Signals that the caller has stopped typing in a conversation.
    /// </summary>
    public async Task StopTyping(string conversationId)
    {
        var userId = Context.User.GetUserId();
        if (string.IsNullOrEmpty(userId) || !await IsParticipantAsync(conversationId, userId))
            return;

        await Clients.OthersInGroup(conversationId).SendAsync("UserStoppedTyping", new { conversationId, userId });
    }

    /// <summary>
    /// Returns true if the user is one of the two participants in the conversation.
    /// </summary>
    private async Task<bool> IsParticipantAsync(string conversationId, string userId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        return conversation != null &&
            (conversation.User1Id == userId || conversation.User2Id == userId);
    }

    /// <summary>
    /// Handles client disconnect.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogError(exception, "Client disconnected with error");
        }

        var userId = Context.User.GetUserId();
        _logger.LogInformation("User {UserId} disconnected from chat", userId ?? "unknown");

        // If that was the user's last connection, they're now offline: stamp last-seen and let their
        // conversation partners know so their UI can switch from "online" to "last seen …".
        if (!string.IsNullOrEmpty(userId) && _presence.Disconnect(userId, Context.ConnectionId))
        {
            var now = DateTime.UtcNow;
            var user = await _userRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                user.LastSeenAt = now;
                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }

            await NotifyPartnersOfPresenceAsync(userId, isOnline: false, lastSeenAt: now);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
