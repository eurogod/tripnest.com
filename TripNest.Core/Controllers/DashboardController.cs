using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Dashboard;
using TripNest.Core.DTOs.TrustScore;
using TripNest.Core.Enums;
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
            // Aggregate in the database (COUNT / scoped filters) instead of loading whole tables into
            // memory. Strongly-typed enum comparisons translate to SQL and also fix the previous
            // stringly-typed check for held escrow ("Held" never matched the enum name HeldInEscrow,
            // so TotalEscrowHeld was always 0).
            var heldEscrows = await _escrowRepository.FindAsync(e => e.Status == EscrowStatus.HeldInEscrow);
            var releasedEscrows = await _escrowRepository.FindAsync(e => e.Status == EscrowStatus.Released);

            var stats = new DashboardStatsResponse
            {
                TotalUsers = await _userRepository.CountAsync(_ => true),
                TotalTenants = await _userRepository.CountAsync(u => u.Role == UserRole.Tenant),
                TotalLandlords = await _userRepository.CountAsync(u => u.Role == UserRole.Landlord),
                TotalAgents = await _userRepository.CountAsync(u => u.Role == UserRole.Agent),
                TotalCaretakers = await _userRepository.CountAsync(u => u.Role == UserRole.Caretaker),
                VerifiedUsers = await _userRepository.CountAsync(u => u.IsVerified),
                TotalProperties = await _propertyRepository.CountAsync(_ => true),
                ActiveProperties = await _propertyRepository.CountAsync(p => p.Status == PropertyStatus.Active),
                TotalBookings = await _bookingRepository.CountAsync(_ => true),
                ConfirmedBookings = await _bookingRepository.CountAsync(b => b.Status == BookingStatus.Confirmed),
                CompletedBookings = await _bookingRepository.CountAsync(b => b.Status == BookingStatus.Completed),
                CancelledBookings = await _bookingRepository.CountAsync(b => b.Status == BookingStatus.Cancelled),
                TotalEscrowHeld = heldEscrows.Sum(e => e.Amount),
                TotalEscrowReleased = releasedEscrows.Sum(e => e.Amount),
                OpenDisputes = await _escrowRepository.CountAsync(e => e.Status == EscrowStatus.Disputed),
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
            var cappedLimit = Math.Clamp(limit ?? 100, 1, 500);
            var logs = (await _auditRepository.GetRecentAsync(cappedLimit, userId)).Cast<object>();

            return Ok(ApiResponse<IEnumerable<object>>.Ok("Audit logs retrieved", logs));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
