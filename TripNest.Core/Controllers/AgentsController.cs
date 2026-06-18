using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Agents;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetAgents([FromQuery] string? serviceArea)
    {
        try
        {
            var agents = await _agentService.GetVerifiedAgentsAsync(serviceArea);
            return Ok(ApiResponse<object>.Ok("Agents retrieved", agents));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agents");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get agent profile with rating
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetAgent(string id)
    {
        try
        {
            var agent = await _agentService.GetAgentProfileAsync(id);
            if (agent == null)
                return NotFound(ApiResponse<object>.NotFound("Agent"));

            return Ok(ApiResponse<object>.Ok("Agent retrieved", agent));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Create a property viewing request
    /// </summary>
    [HttpPost("{id}/viewing-requests")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> CreateViewingRequest(string id, [FromBody] CreateViewingRequestRequest request)
    {
        try
        {
            var tenantId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var viewingRequest = await _agentService.CreateViewingRequestAsync(id, request.PropertyId, request.ScheduledAt, tenantId, request.Notes);
            var viewingId = ((dynamic)viewingRequest).Id;
            return Created($"api/viewing-requests/{viewingId}", ApiResponse<object>.Created("Viewing Request", viewingRequest));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating viewing request");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Update viewing request status
    /// </summary>
    [HttpPatch("viewing-requests/{id}/status")]
    [Authorize(Roles = "Agent,Tenant")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateViewingRequestStatus(string id, [FromBody] UpdateViewingRequestStatusRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _agentService.UpdateViewingRequestStatusAsync(id, request.Status, userId);
            return Ok(ApiResponse<object>.Ok("Status updated", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
