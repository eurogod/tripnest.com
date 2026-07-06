using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TripNest.Core.DTOs.Assistant;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;

    public AssistantController(IAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    /// <summary>
    /// Ask the TripNest Assistant a question. Answers are grounded in platform rules and the
    /// caller's own bookings/escrow/verification; when a human is needed it opens a support
    /// ticket and notifies admins. Rate-limited so one user can't burn the AI quota.
    /// </summary>
    [HttpPost("ask")]
    [EnableRateLimiting("ai")]
    [ProducesResponseType(typeof(ApiResponse<AssistantReplyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AssistantReplyResponse>>> Ask([FromBody] AskAssistantRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var reply = await _assistantService.AskAsync(userId, request.Question);
        return Ok(ApiResponse<AssistantReplyResponse>.Ok("Assistant replied", reply));
    }

    /// <summary>The caller's conversation with the assistant, oldest first.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<AssistantHistoryItem>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AssistantHistoryItem>>>> History([FromQuery] int limit = 50)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var history = await _assistantService.GetHistoryAsync(userId, limit);
        return Ok(ApiResponse<List<AssistantHistoryItem>>.Ok("Assistant history retrieved", history));
    }
}
