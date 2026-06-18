using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Chat;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's conversations
    /// </summary>
    [HttpGet("conversations/mine")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyConversations()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var conversations = await _chatService.GetUserConversationsAsync(userId);
            return Ok(ApiResponse<object>.Ok("Conversations retrieved", conversations));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversations");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Start a new conversation
    /// </summary>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> StartConversation([FromBody] StartConversationRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var conversation = await _chatService.StartConversationAsync(userId, request.OtherUserId, request.PropertyId);
            var convId = ((dynamic)conversation).Id;
            return Created($"api/conversations/{convId}", ApiResponse<object>.Created("Conversation", conversation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting conversation");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get conversation details and participants
    /// </summary>
    [HttpGet("conversations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetConversation(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var conversation = await _chatService.GetConversationAsync(id, userId);
            if (conversation == null)
                return NotFound(ApiResponse<object>.NotFound("Conversation"));

            return Ok(ApiResponse<object>.Ok("Conversation retrieved", conversation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving conversation");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get message history for a conversation
    /// </summary>
    [HttpGet("conversations/{id}/messages")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetConversationMessages(string id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var messages = await _chatService.GetConversationMessagesAsync(id, userId, page, pageSize);
            return Ok(ApiResponse<object>.Ok("Messages retrieved", messages));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Send a message (REST endpoint - for non-realtime clients)
    /// </summary>
    [HttpPost("conversations/{id}/messages")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> SendMessage(string id, [FromBody] SendMessageRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var message = await _chatService.SendMessageAsync(id, userId, request.Body);
            var msgId = ((dynamic)message).Id;
            return Created($"api/messages/{msgId}", ApiResponse<object>.Created("Message", message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark message as read
    /// </summary>
    [HttpPatch("messages/{id}/read")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> MarkMessageAsRead(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _chatService.MarkMessageAsReadAsync(id, userId);
            return Ok(ApiResponse<object>.Ok("Message marked as read", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking message as read");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Mark all messages in conversation as read
    /// </summary>
    [HttpPatch("conversations/{id}/mark-read")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> MarkConversationAsRead(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _chatService.MarkConversationAsReadAsync(id, userId);
            return Ok(ApiResponse<object>.Ok("Conversation marked as read", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation as read");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Delete a conversation
    /// </summary>
    [HttpDelete("conversations/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteConversation(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _chatService.DeleteConversationAsync(id, userId);
            return Ok(ApiResponse<object>.Ok("Conversation deleted", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting conversation");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
