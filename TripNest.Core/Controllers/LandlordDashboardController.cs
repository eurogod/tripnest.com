using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/landlord")]
[Authorize(Roles = "Landlord")]
[Produces("application/json")]
public class LandlordDashboardController : ControllerBase
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IReceiptRepository _receiptRepository;
    private readonly ILogger<LandlordDashboardController> _logger;

    public LandlordDashboardController(
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IReceiptRepository receiptRepository,
        ILogger<LandlordDashboardController> logger)
    {
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _receiptRepository = receiptRepository;
        _logger = logger;
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetStats()
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var properties = (await _propertyRepository.GetByUserIdAsync(landlordId)).ToList();
            var propertyIds = properties.Select(p => p.Id).ToHashSet();

            var allBookings = new List<Models.Booking>();
            foreach (var propertyId in propertyIds)
            {
                var bookings = await _bookingRepository.GetByPropertyIdAsync(propertyId);
                allBookings.AddRange(bookings);
            }

            var activeStatuses = new[] { BookingStatus.Confirmed, BookingStatus.CheckedIn };

            var stats = new
            {
                TotalProperties = properties.Count,
                ActiveProperties = properties.Count(p => p.Status == PropertyStatus.Active),
                TotalBookings = allBookings.Count,
                ActiveBookings = allBookings.Count(b => activeStatuses.Contains(b.Status)),
                CompletedBookings = allBookings.Count(b => b.Status == BookingStatus.Completed)
            };

            return Ok(ApiResponse<object>.Ok("Stats retrieved", stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landlord stats");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("earnings")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetEarnings()
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var properties = await _propertyRepository.GetByUserIdAsync(landlordId);
            var propertyIds = properties.Select(p => p.Id).ToHashSet();

            var allBookings = new List<Models.Booking>();
            foreach (var propertyId in propertyIds)
            {
                var bookings = await _bookingRepository.GetByPropertyIdAsync(propertyId);
                allBookings.AddRange(bookings);
            }

            var completedBookingIds = allBookings
                .Where(b => b.Status == BookingStatus.Completed)
                .Select(b => b.Id)
                .ToHashSet();

            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);
            var endOfLastMonth = startOfThisMonth;

            decimal totalEarnings = 0m;
            decimal thisMonthEarnings = 0m;
            decimal lastMonthEarnings = 0m;

            foreach (var bookingId in completedBookingIds)
            {
                var receipts = await _receiptRepository.GetByBookingIdAsync(bookingId);
                foreach (var receipt in receipts)
                {
                    totalEarnings += receipt.Amount;

                    if (receipt.CreatedAt >= startOfThisMonth)
                        thisMonthEarnings += receipt.Amount;

                    if (receipt.CreatedAt >= startOfLastMonth && receipt.CreatedAt < endOfLastMonth)
                        lastMonthEarnings += receipt.Amount;
                }
            }

            var earnings = new
            {
                TotalEarnings = totalEarnings,
                ThisMonthEarnings = thisMonthEarnings,
                LastMonthEarnings = lastMonthEarnings
            };

            return Ok(ApiResponse<object>.Ok("Earnings retrieved", earnings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving landlord earnings");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("properties/performance")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetPropertiesPerformance()
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var properties = await _propertyRepository.GetByUserIdAsync(landlordId);

            var performanceList = new List<object>();

            foreach (var property in properties)
            {
                var bookings = (await _bookingRepository.GetByPropertyIdAsync(property.Id)).ToList();
                var completedBookings = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();

                decimal totalRevenue = 0m;
                foreach (var booking in completedBookings)
                {
                    var receipts = await _receiptRepository.GetByBookingIdAsync(booking.Id);
                    totalRevenue += receipts.Sum(r => r.Amount);
                }

                performanceList.Add(new
                {
                    PropertyId = property.Id,
                    property.Title,
                    TotalBookings = bookings.Count,
                    CompletedBookings = completedBookings.Count,
                    TotalRevenue = totalRevenue,
                    AverageRating = 0.0
                });
            }

            return Ok(ApiResponse<IEnumerable<object>>.Ok("Property performance retrieved", performanceList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property performance");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
