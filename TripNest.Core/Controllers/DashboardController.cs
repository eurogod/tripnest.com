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

    /// <summary>Stamps the admin's first response on a ticket — the urgent-SLA clock's stop line.
    /// Idempotent: only the first acknowledgement counts.</summary>
    [HttpPost("support-tickets/{ticketId}/ack")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> AcknowledgeTicket(string ticketId)
    {
        var tickets = HttpContext.RequestServices.GetRequiredService<IRepository<Models.SupportTicket>>();
        var ticket = await tickets.GetByIdAsync(ticketId);
        if (ticket is null)
            return NotFound(ApiResponse<object>.NotFound("Support ticket"));

        if (ticket.FirstRespondedAt is null)
        {
            ticket.FirstRespondedAt = DateTime.UtcNow;
            await tickets.UpdateAsync(ticket);
            await tickets.SaveChangesAsync();
        }

        return Ok(ApiResponse<object>.Ok("Ticket acknowledged", new
        {
            firstRespondedAt = ticket.FirstRespondedAt,
            responseSeconds = (int)(ticket.FirstRespondedAt.Value - ticket.CreatedAt).TotalSeconds
        }));
    }

    // ------------------------------------------------- dynamic-pricing demand events

    public record UpsertDemandEventRequest(string Name, string Location, DateTime StartDate, DateTime EndDate, decimal UpliftPercent);

    /// <summary>Demand events feeding dynamic pricing (festivals, conferences, holiday peaks).</summary>
    [HttpGet("demand-events")]
    [ProducesResponseType(typeof(ApiResponse<List<Models.DemandEvent>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<Models.DemandEvent>>>> GetDemandEvents()
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Models.DemandEvent>>();
        var events = (await repository.GetAllAsync()).OrderBy(e => e.StartDate).ToList();
        return Ok(ApiResponse<List<Models.DemandEvent>>.Ok("Demand events retrieved", events));
    }

    /// <summary>Creates a demand event: dynamically priced listings whose location matches get the
    /// uplift for the event's dates.</summary>
    [HttpPost("demand-events")]
    [ProducesResponseType(typeof(ApiResponse<Models.DemandEvent>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<Models.DemandEvent>>> CreateDemandEvent([FromBody] UpsertDemandEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Location))
            return BadRequest(ApiResponse<object>.BadRequest("Name and location are required"));
        if (request.EndDate <= request.StartDate)
            return BadRequest(ApiResponse<object>.BadRequest("End date must be after the start date"));
        if (request.UpliftPercent is < 0 or > 200)
            return BadRequest(ApiResponse<object>.BadRequest("Uplift must be between 0 and 200 percent"));

        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Models.DemandEvent>>();
        var demandEvent = new Models.DemandEvent
        {
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            StartDate = DateTime.SpecifyKind(request.StartDate.Date, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(request.EndDate.Date, DateTimeKind.Utc),
            UpliftPercent = request.UpliftPercent
        };
        await repository.AddAsync(demandEvent);
        await repository.SaveChangesAsync();
        return StatusCode(201, ApiResponse<Models.DemandEvent>.Created("Demand event", demandEvent));
    }

    /// <summary>Removes a demand event (rates fall back to organic demand immediately).</summary>
    [HttpDelete("demand-events/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteDemandEvent(string id)
    {
        var repository = HttpContext.RequestServices.GetRequiredService<IRepository<Models.DemandEvent>>();
        var demandEvent = await repository.GetByIdAsync(id);
        if (demandEvent is null)
            return NotFound(ApiResponse<object>.NotFound("Demand event"));
        await repository.DeleteAsync(demandEvent);
        await repository.SaveChangesAsync();
        return Ok(ApiResponse<object>.Ok("Demand event removed", new { }));
    }
}
