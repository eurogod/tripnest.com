using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using TripNest.Core.DTOs.Chat;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Hubs;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, IHubContext<ChatHub> hubContext, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's conversations
    /// </summary>
    [HttpGet("conversations/mine")]
    [ProducesResponseType(typeof(ApiResponse<List<ConversationResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ConversationResponse>>>> GetMyConversations()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<List<ConversationResponse>>.UnAuthorized());

            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(ApiResponse<List<ConversationResponse>>.Ok("Conversations retrieved", conversations));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return StatusCode(500, ApiResponse<List<ConversationResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Start a new conversation
    /// </summary>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> StartConversation([FromBody] StartConversationRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ConversationResponse>.UnAuthorized());

            var conversation = await _chatService.StartConversationAsync(userId, request.OtherUserId, request.PropertyId);
            return Created($"api/conversations/{conversation.ConversationId}", ApiResponse<ConversationResponse>.Created("Conversation", conversation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ConversationResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation");
            return StatusCode(500, ApiResponse<ConversationResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Get conversation details and participants
    /// </summary>
    [HttpGet("conversations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> GetConversation(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ConversationResponse>.UnAuthorized());

            var conversation = await _chatService.GetConversationAsync(id, userId);
            if (conversation == null)
                return NotFound(ApiResponse<ConversationResponse>.NotFound("Conversation"));

            return Ok(ApiResponse<ConversationResponse>.Ok("Conversation retrieved", conversation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation");
            return StatusCode(500, ApiResponse<ConversationResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Get message history for a conversation
    /// </summary>
    [HttpGet("conversations/{id}/messages")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MessageResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MessageResponse>>>> GetConversationMessages(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<PagedResult<MessageResponse>>.UnAuthorized());

            var messages = await _chatService.GetConversationMessagesAsync(id, userId, page, pageSize);
            return Ok(ApiResponse<PagedResult<MessageResponse>>.Ok("Messages retrieved", messages));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages");
            return StatusCode(500, ApiResponse<PagedResult<MessageResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Send a message (REST endpoint - for non-realtime clients)
    /// </summary>
    [HttpPost("conversations/{id}/messages")]
    [ProducesResponseType(typeof(ApiResponse<MessageResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<MessageResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> SendMessage(string id, [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<MessageResponse>.UnAuthorized());

            var message = await _chatService.SendMessageAsync(id, userId, request.Body);

            // Push to any connected SignalR clients so REST-sent messages appear in real time,
            // matching the payload the hub broadcasts on the same event.
            await _hubContext.Clients.Group(id).SendAsync("ReceiveMessage", message);

            return Created($"api/messages/{message.MessageId}", ApiResponse<MessageResponse>.Created("Message", message));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<MessageResponse>.Forbidden("You are not a participant in this conversation"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MessageResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, ApiResponse<MessageResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark message as read
    /// </summary>
    [HttpPatch("messages/{id}/read")]
    [ProducesResponseType(typeof(ApiResponse<MessageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<MessageResponse>>> MarkMessageAsRead(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<MessageResponse>.UnAuthorized());

            await _chatService.MarkMessageAsReadAsync(id, userId);
            return Ok(ApiResponse<MessageResponse>.Ok("Message marked as read", null));
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, ApiResponse<MessageResponse>.Forbidden("You are not a participant in this conversation"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MessageResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read");
            return StatusCode(500, ApiResponse<MessageResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark all messages in conversation as read
    /// </summary>
    [HttpPatch("conversations/{id}/mark-read")]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> MarkConversationAsRead(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ConversationResponse>.UnAuthorized());

            await _chatService.MarkConversationAsReadAsync(id, userId);
            return Ok(ApiResponse<ConversationResponse>.Ok("Conversation marked as read", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation as read");
            return StatusCode(500, ApiResponse<ConversationResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("conversations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<ConversationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ConversationResponse>>> DeleteConversation(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ConversationResponse>.UnAuthorized());

            await _chatService.DeleteConversationAsync(id, userId);
            return Ok(ApiResponse<ConversationResponse>.Ok("Conversation deleted", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation");
            return StatusCode(500, ApiResponse<ConversationResponse>.InternalServerError());
        }
    }
}
