using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Agents;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

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
    /// Get list of verified agents, optionally filtered by service area
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AgentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AgentResponse>>>> GetAgents([FromQuery] string? serviceArea)
    {
        try
        {
            var agents = await _agentService.GetVerifiedAgentsAsync(serviceArea);
            return Ok(ApiResponse<List<AgentResponse>>.Ok("Agents retrieved", agents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents");
            return StatusCode(500, ApiResponse<List<AgentResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Get agent profile with rating
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AgentResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AgentResponse>>> GetAgent(string id)
    {
        try
        {
            var agent = await _agentService.GetAgentProfileAsync(id);
            if (agent == null)
                return NotFound(ApiResponse<AgentResponse>.NotFound("Agent"));

            return Ok(ApiResponse<AgentResponse>.Ok("Agent retrieved", agent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent");
            return StatusCode(500, ApiResponse<AgentResponse>.InternalServerError());
        }
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
        try
        {
            var tenantId = User.GetUserId();
            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

            var viewingRequest = await _agentService.CreateViewingRequestAsync(id, request.PropertyId, request.ScheduledAt, tenantId, request.Notes);
            return Created($"api/viewing-requests/{viewingRequest.ViewingRequestId}", ApiResponse<ViewingRequestResponse>.Created("Viewing Request", viewingRequest));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ViewingRequestResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating viewing request");
            return StatusCode(500, ApiResponse<ViewingRequestResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Update viewing request status
    /// </summary>
    [HttpPatch("viewing-requests/{id}/status")]
    [Authorize(Roles = "Agent,Tenant")]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ViewingRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ViewingRequestResponse>>> UpdateViewingRequestStatus(string id, [FromBody] UpdateViewingRequestStatusRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ViewingRequestResponse>.UnAuthorized());

            await _agentService.UpdateViewingRequestStatusAsync(id, request.Status, userId);
            return Ok(ApiResponse<ViewingRequestResponse>.Ok("Status updated", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status");
            return StatusCode(500, ApiResponse<ViewingRequestResponse>.InternalServerError());
        }
    }
}
