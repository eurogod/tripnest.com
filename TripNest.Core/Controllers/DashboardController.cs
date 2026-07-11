using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Dashboard;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardStatsService _statsService;
    private readonly IAssistantService _assistantService;
    private readonly IAuditLogRepository _auditRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardStatsService statsService,
        IAssistantService assistantService,
        IAuditLogRepository auditRepository,
        ILogger<DashboardController> logger)
    {
        _statsService = statsService;
        _assistantService = assistantService;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    /// <summary>Open support tickets (assistant escalations), oldest first.</summary>
    [HttpGet("support-tickets")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TripNest.Core.DTOs.Assistant.SupportTicketResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<TripNest.Core.DTOs.Assistant.SupportTicketResponse>>>> GetSupportTickets([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tickets = await _assistantService.GetOpenTicketsAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<TripNest.Core.DTOs.Assistant.SupportTicketResponse>>.Ok("Support tickets retrieved", tickets));
    }

    /// <summary>Marks a support ticket resolved and notifies the user (idempotent).</summary>
    [HttpPost("support-tickets/{ticketId}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ResolveSupportTicket(string ticketId)
    {
        var adminId = User.GetUserId();
        if (string.IsNullOrEmpty(adminId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _assistantService.ResolveTicketAsync(ticketId, adminId);
        return Ok(ApiResponse<object>.Ok("Support ticket resolved", new { }));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardStatsResponse>>> GetStats()
    {
        var stats = await _statsService.GetStatsAsync();
        return Ok(ApiResponse<DashboardStatsResponse>.Ok("Dashboard stats retrieved", stats));
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetAuditLogs([FromQuery] string? userId = null, [FromQuery] int? limit = 100)
    {
        var cappedLimit = Math.Clamp(limit ?? 100, 1, 500);
        var logs = (await _auditRepository.GetRecentAsync(cappedLimit, userId)).Cast<object>();

        return Ok(ApiResponse<IEnumerable<object>>.Ok("Audit logs retrieved", logs));
    }
}
