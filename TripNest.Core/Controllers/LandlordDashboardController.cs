using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

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
    private readonly IRepository<Payout> _payoutRepository;
    private readonly ILogger<LandlordDashboardController> _logger;

    public LandlordDashboardController(
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IReceiptRepository receiptRepository,
        IRepository<Payout> payoutRepository,
        ILogger<LandlordDashboardController> logger)
    {
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _receiptRepository = receiptRepository;
        _payoutRepository = payoutRepository;
        _logger = logger;
    }

    // NOTE: GET /api/landlord/stats overlaps with GET /api/personaldashboard/landlord (a superset).
    // It's kept because the frontend's getOverview() composes from both (see FRONTEND_INTEGRATION.md).
    // If the frontend consolidates onto /personaldashboard/landlord, this can be removed.
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetStats()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var properties = (await _propertyRepository.GetByUserIdAsync(landlordId)).ToList();
        var propertyIds = properties.Select(p => p.Id).ToList();

        // One query for all of this landlord's bookings instead of a query per property (N+1).
        var allBookings = (await _bookingRepository.FindAsync(b => propertyIds.Contains(b.PropertyId))).ToList();

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

    [HttpGet("earnings")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetEarnings()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var now = DateTime.UtcNow;
        var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfLastMonth = startOfThisMonth.AddMonths(-1);
        var endOfLastMonth = startOfThisMonth;

        decimal totalEarnings = 0m;
        decimal thisMonthEarnings = 0m;
        decimal lastMonthEarnings = 0m;

        // Earnings track the landlord's payouts — one is created for every released escrow (and paid
        // rent invoice). Gross is the booking revenue the host earned; the platform fee is a separate
        // line in the transactions table. (Receipts are a tenant-side artefact and aren't produced
        // here, which is why this used to always read zero.)
        var payouts = await _payoutRepository.FindAsync(p => p.LandlordId == landlordId);
        foreach (var payout in payouts)
        {
            totalEarnings += payout.GrossAmount;

            if (payout.CreatedAt >= startOfThisMonth)
                thisMonthEarnings += payout.GrossAmount;

            if (payout.CreatedAt >= startOfLastMonth && payout.CreatedAt < endOfLastMonth)
                lastMonthEarnings += payout.GrossAmount;
        }

        var earnings = new
        {
            TotalEarnings = totalEarnings,
            ThisMonthEarnings = thisMonthEarnings,
            LastMonthEarnings = lastMonthEarnings
        };

        return Ok(ApiResponse<object>.Ok("Earnings retrieved", earnings));
    }

    [HttpGet("properties/performance")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetPropertiesPerformance()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var properties = (await _propertyRepository.GetByUserIdAsync(landlordId)).ToList();
        var propertyIds = properties.Select(p => p.Id).ToList();

        // Pull all bookings for these properties, then all receipts for their completed bookings,
        // in two queries — instead of a booking query per property and a receipt query per booking.
        var bookings = (await _bookingRepository.FindAsync(b => propertyIds.Contains(b.PropertyId))).ToList();
        var completedBookingIds = bookings.Where(b => b.Status == BookingStatus.Completed).Select(b => b.Id).ToList();
        var receiptsByBooking = (await _receiptRepository.GetByBookingIdsAsync(completedBookingIds))
            .GroupBy(r => r.BookingId)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));

        var bookingsByProperty = bookings.GroupBy(b => b.PropertyId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var performanceList = new List<object>();
        foreach (var property in properties)
        {
            var propertyBookings = bookingsByProperty.TryGetValue(property.Id, out var pb) ? pb : new List<Models.Booking>();
            var completedBookings = propertyBookings.Where(b => b.Status == BookingStatus.Completed).ToList();
            var totalRevenue = completedBookings.Sum(b => receiptsByBooking.TryGetValue(b.Id, out var amt) ? amt : 0m);

            performanceList.Add(new
            {
                PropertyId = property.Id,
                property.Title,
                TotalBookings = propertyBookings.Count,
                CompletedBookings = completedBookings.Count,
                TotalRevenue = totalRevenue,
                AverageRating = 0.0
            });
        }

        return Ok(ApiResponse<IEnumerable<object>>.Ok("Property performance retrieved", performanceList));
    }
}
