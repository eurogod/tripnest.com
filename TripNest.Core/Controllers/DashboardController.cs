using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Dashboard;
using TripNest.Core.DTOs.TrustScore;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IAuditLogRepository _auditRepository;
    private readonly ITrustScoreSnapshotRepository _trustScoreRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IUserRepository userRepository,
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IEscrowRepository escrowRepository,
        IAuditLogRepository auditRepository,
        ITrustScoreSnapshotRepository trustScoreRepository,
        ILogger<DashboardController> logger)
    {
        _userRepository = userRepository;
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _escrowRepository = escrowRepository;
        _auditRepository = auditRepository;
        _trustScoreRepository = trustScoreRepository;
        _logger = logger;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<DashboardStatsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<DashboardStatsResponse>>> GetStats()
    {
        try
        {
            var allUsers = await _userRepository.GetAllAsync();
            var allProperties = await _propertyRepository.GetAllAsync();
            var allBookings = await _bookingRepository.GetAllAsync();
            var allEscrows = await _escrowRepository.GetAllAsync();

            var stats = new DashboardStatsResponse
            {
                TotalUsers = allUsers.Count(),
                TotalTenants = allUsers.Count(u => u.Role.ToString() == "Tenant"),
                TotalLandlords = allUsers.Count(u => u.Role.ToString() == "Landlord"),
                TotalAgents = allUsers.Count(u => u.Role.ToString() == "Agent"),
                TotalCaretakers = allUsers.Count(u => u.Role.ToString() == "Caretaker"),
                VerifiedUsers = allUsers.Count(u => u.IsVerified),
                TotalProperties = allProperties.Count(),
                ActiveProperties = allProperties.Count(p => p.Status.ToString() == "Active"),
                TotalBookings = allBookings.Count(),
                ConfirmedBookings = allBookings.Count(b => b.Status.ToString() == "Confirmed"),
                CompletedBookings = allBookings.Count(b => b.Status.ToString() == "Completed"),
                CancelledBookings = allBookings.Count(b => b.Status.ToString() == "Cancelled"),
                TotalEscrowHeld = allEscrows.Where(e => e.Status.ToString() == "Held").Sum(e => e.Amount),
                TotalEscrowReleased = allEscrows.Where(e => e.Status.ToString() == "Released").Sum(e => e.Amount),
                OpenDisputes = allEscrows.Count(e => e.Status.ToString() == "Disputed"),
                AverageTrustScore = 75m
            };

            return Ok(ApiResponse<DashboardStatsResponse>.Ok("Dashboard stats retrieved", stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dashboard stats");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetAuditLogs([FromQuery] string? userId = null, [FromQuery] int? limit = 100)
    {
        try
        {
            IEnumerable<object> logs;
            if (!string.IsNullOrEmpty(userId))
            {
                var userLogs = await _auditRepository.GetByUserIdAsync(userId);
                logs = userLogs.Take(limit ?? 100).Cast<object>();
            }
            else
            {
                logs = (await _auditRepository.GetAllAsync()).OrderByDescending(l => l.CreatedAt).Take(limit ?? 100).Cast<object>();
            }

            return Ok(ApiResponse<IEnumerable<object>>.Ok("Audit logs retrieved", logs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
