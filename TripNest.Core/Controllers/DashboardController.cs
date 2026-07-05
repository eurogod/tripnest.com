using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Dashboard;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardStatsService _statsService;
    private readonly IAuditLogRepository _auditRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardStatsService statsService,
        IAuditLogRepository auditRepository,
        ILogger<DashboardController> logger)
    {
        _statsService = statsService;
        _auditRepository = auditRepository;
        _logger = logger;
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
