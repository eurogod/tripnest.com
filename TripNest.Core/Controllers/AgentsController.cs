using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using TripNest.Core.DTOs.Agents;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AgentsController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Get list of verified agents, optionally filtered by service area (paged)
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AgentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AgentResponse>>>> GetAgents(
        [FromQuery] string? serviceArea, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var agents = await _agentService.GetVerifiedAgentsAsync(serviceArea, page, pageSize);
        return Ok(ApiResponse<PagedResult<AgentResponse>>.Ok("Agents retrieved", agents));
    }

    /// <summary>
    /// The caller's own directory profile (404 until they create one via PUT /api/agents/me).
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "Agent,Admin")]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> GetMyProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<AgentResponse>.UnAuthorized());

        var profile = await _agentService.GetMyProfileAsync(userId);
        if (profile is null)
            return NotFound(ApiResponse<AgentResponse>.NotFound("Agent profile"));

        return Ok(ApiResponse<AgentResponse>.Ok("Agent profile retrieved", profile));
    }

    /// <summary>
    /// Create or update the caller's public directory profile — without it an Agent-role account
    /// never appears in the agents list. Requires identity verification, like other agent actions.
    /// </summary>
    [HttpPut("me")]
    [Authorize(Roles = "Agent,Admin")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> UpsertMyProfile([FromBody] UpsertAgentProfileRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<AgentResponse>.UnAuthorized());

        var profile = await _agentService.UpsertMyProfileAsync(userId, request);
        return Ok(ApiResponse<AgentResponse>.Ok("Agent profile saved", profile));
    }

    /// <summary>
    /// Get agent profile with rating
    /// </summary>
    [HttpGet("{id}")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> GetAgent(string id)
    {
        var agent = await _agentService.GetAgentProfileAsync(id);
        if (agent == null)
            return NotFound(ApiResponse<AgentResponse>.NotFound("Agent"));

        return Ok(ApiResponse<AgentResponse>.Ok("Agent retrieved", agent));
    }

    /// <summary>
    /// Create a property viewing request
    /// </summary>
    [HttpPost("{id}/viewing-requests")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ViewingRequestResponse>>> CreateViewingRequest(string id, [FromBody] CreateViewingRequestRequest request)
    {
        var tenantId = User.GetUserId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

        var viewingRequest = await _agentService.CreateViewingRequestAsync(id, request.PropertyId, request.ScheduledAt, tenantId, request.Notes);
        return Created($"api/viewing-requests/{viewingRequest.ViewingRequestId}", ApiResponse<ViewingRequestResponse>.Created("Viewing Request", viewingRequest));
    }

    /// <summary>
    /// Update viewing request status
    /// </summary>
    [HttpPatch("viewing-requests/{id}/status")]
    [Authorize(Roles = "Agent,Tenant")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ViewingRequestResponse>>> UpdateViewingRequestStatus(string id, [FromBody] UpdateViewingRequestStatusRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

        await _agentService.UpdateViewingRequestStatusAsync(id, request.Status, userId);
        return Ok(ApiResponse<ViewingRequestResponse>.Ok("Status updated", null));
    }

    /// <summary>
    /// Decline a pending viewing request (assigned agent only)
    /// </summary>
    [HttpPatch("viewing-requests/{id}/decline")]
    [Authorize(Roles = "Agent")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ViewingRequestResponse>>> DeclineViewingRequest(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

        await _agentService.DeclineViewingRequestAsync(id, userId);
        return Ok(ApiResponse<ViewingRequestResponse>.Ok("Viewing request declined", null));
    }

    /// <summary>
    /// Submit review for a completed viewing (requesting tenant only)
    /// </summary>
    [HttpPost("viewing-requests/{id}/review")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ViewingRequestResponse>>> ReviewViewing(string id, [FromBody] SubmitViewingReviewRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

        await _agentService.SubmitViewingReviewAsync(id, userId, request.Rating, request.Comment);
        return StatusCode(201, ApiResponse<ViewingRequestResponse>.Ok("Review submitted successfully"));
    }

    /// <summary>
    /// Viewing requests the caller is part of — as the requesting tenant and/or the assigned agent.
    /// </summary>
    [HttpGet("viewing-requests/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ViewingRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ViewingRequestResponse>>>> GetMyViewingRequests()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<List<ViewingRequestResponse>>.UnAuthorized());

            var requests = await _agentService.GetMyViewingRequestsAsync(userId);
            return Ok(ApiResponse<List<ViewingRequestResponse>>.Ok("Viewing requests retrieved", requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving viewing requests");
            return StatusCode(500, ApiResponse<List<ViewingRequestResponse>>.InternalServerError());
        }
    }
}
