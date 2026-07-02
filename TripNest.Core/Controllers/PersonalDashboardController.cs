using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class PersonalDashboardController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IWalkthroughRepository _walkthroughRepository;
    private readonly ITrustScoreSnapshotRepository _trustScoreRepository;
    private readonly ILogger<PersonalDashboardController> _logger;

    public PersonalDashboardController(
        IUserRepository userRepository,
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IEscrowRepository escrowRepository,
        IWalkthroughRepository walkthroughRepository,
        ITrustScoreSnapshotRepository trustScoreRepository,
        ILogger<PersonalDashboardController> logger)
    {
        _userRepository = userRepository;
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _escrowRepository = escrowRepository;
        _walkthroughRepository = walkthroughRepository;
        _trustScoreRepository = trustScoreRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get tenant dashboard with bookings and stay history
    /// </summary>
    [HttpGet("tenant")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetTenantDashboard()
    {
        try
        {
            var userId = User.GetUserId()
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            // Only this tenant's bookings, filtered in the database.
            var tenantBookings = (await _bookingRepository.FindAsync(b => b.TenantId == userId))
                .OrderByDescending(b => b.CreatedAt).ToList();

            var dashboard = new
            {
                ActiveBookings = tenantBookings.Count(b => b.Status.ToString() == "Confirmed"),
                CompletedStays = tenantBookings.Count(b => b.Status.ToString() == "Completed"),
                CancelledBookings = tenantBookings.Count(b => b.Status.ToString() == "Cancelled"),
                UpcomingCheckIns = tenantBookings.Where(b => b.Status.ToString() == "Confirmed" && b.CheckInDate > DateTime.UtcNow).Count(),
                RecentBookings = tenantBookings.Take(5).Select(b => new
                {
                    b.Id,
                    b.PropertyId,
                    b.CheckInDate,
                    b.CheckOutDate,
                    b.Status,
                    b.TotalAmount
                }),
                TotalSpent = tenantBookings.Where(b => b.Status.ToString() == "Completed").Sum(b => b.TotalAmount)
            };

            return Ok(ApiResponse<object>.Ok("Tenant dashboard retrieved", dashboard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tenant dashboard");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get landlord dashboard with properties and earnings
    /// </summary>
    [HttpGet("landlord")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetLandlordDashboard()
    {
        try
        {
            var userId = User.GetUserId()
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            // Scope each query to this landlord in the database instead of loading whole tables.
            var landlordProperties = (await _propertyRepository.FindAsync(p => p.UserId == userId)).ToList();
            var propertyIds = landlordProperties.Select(p => p.Id).ToList();
            var landlordBookings = (await _bookingRepository.FindAsync(b => propertyIds.Contains(b.PropertyId))).ToList();
            var bookingIds = landlordBookings.Select(b => b.Id).ToList();
            var landlordEscrows = (await _escrowRepository.FindAsync(e => bookingIds.Contains(e.BookingId))).ToList();

            var dashboard = new
            {
                TotalProperties = landlordProperties.Count(),
                ActiveProperties = landlordProperties.Count(p => p.Status.ToString() == "Active"),
                TotalBookings = landlordBookings.Count(),
                ConfirmedBookings = landlordBookings.Count(b => b.Status.ToString() == "Confirmed"),
                CompletedBookings = landlordBookings.Count(b => b.Status.ToString() == "Completed"),
                PendingWalkthroughs = landlordProperties.Count(p => p.Walkthroughs.Any()),
                EscrowHeld = landlordEscrows.Where(e => e.Status.ToString() == "Held").Sum(e => e.Amount),
                EscrowReleased = landlordEscrows.Where(e => e.Status.ToString() == "Released").Sum(e => e.Amount),
                OpenDisputes = landlordEscrows.Count(e => e.Status.ToString() == "Disputed"),
                RecentProperties = landlordProperties.OrderByDescending(p => p.CreatedAt).Take(5).Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Status,
                    p.DailyRate,
                    p.MonthlyRent
                })
            };

            return Ok(ApiResponse<object>.Ok("Landlord dashboard retrieved", dashboard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landlord dashboard");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get agent dashboard with viewing requests and properties
    /// </summary>
    [HttpGet("agent")]
    [Authorize(Roles = "Agent")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetAgentDashboard()
    {
        try
        {
            var userId = User.GetUserId()
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            // Aggregate directly over the walkthrough table in the database. (Previously this loaded
            // all properties and read p.Walkthroughs — a navigation that is never eager-loaded here —
            // so every metric was silently 0.)
            var stats = await _walkthroughRepository.GetStatsAsync(DateTime.UtcNow.AddDays(-30));
            var totalProperties = await _propertyRepository.CountAsync(_ => true);

            var dashboard = new
            {
                TotalWalkthroughs = stats.Total,
                PropertiesWithWalkthroughs = stats.DistinctPropertyCount,
                PropertiesWithoutWalkthroughs = totalProperties - stats.DistinctPropertyCount,
                RecentWalkthroughsCount = stats.RecentCount,
                RecentActivity = new
                {
                    LastWalkthroughDate = stats.LastCreatedAt,
                    TotalVideoHours = Math.Round(stats.TotalDurationSeconds / 3600.0, 2)
                }
            };

            return Ok(ApiResponse<object>.Ok("Agent dashboard retrieved", dashboard));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agent dashboard");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get caretaker dashboard with service requests and ratings
    /// </summary>
    [HttpGet("caretaker")]
    [Authorize(Roles = "Caretaker")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public Task<ActionResult<ApiResponse<object>>> GetCaretakerDashboard()
    {
        try
        {
            var userId = User.GetUserId()
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Task.FromResult<ActionResult<ApiResponse<object>>>(Unauthorized(ApiResponse<object>.UnAuthorized()));

            var dashboard = new
            {
                TotalServiceRequests = 0,
                ActiveServiceRequests = 0,
                CompletedServiceRequests = 0,
                PendingRequests = 0,
                AverageRating = 0m,
                TotalReviews = 0,
                EarningsThisMonth = 0m,
                RecentActivity = new
                {
                    Message = "Track your service requests and earnings here"
                }
            };

            return Task.FromResult<ActionResult<ApiResponse<object>>>(Ok(ApiResponse<object>.Ok("Caretaker dashboard retrieved", dashboard)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretaker dashboard");
            return Task.FromResult<ActionResult<ApiResponse<object>>>(StatusCode(500, ApiResponse<object>.InternalServerError()));
        }
    }
}
