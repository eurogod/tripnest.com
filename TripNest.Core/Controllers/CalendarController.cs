using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/calendar")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendarService;

    public CalendarController(ICalendarService calendarService) => _calendarService = calendarService;

    /// <summary>
    /// The priced availability calendar for a listing for a given month, with weekend / blocked /
    /// maintenance / booked day flags. Defaults to the current month.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<CalendarMonthResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<CalendarMonthResponse>>> GetMonth(
        [FromQuery] string propertyId,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<CalendarMonthResponse>.UnAuthorized());

        var now = DateTime.UtcNow;
        var calendar = await _calendarService.GetMonthAsync(propertyId, year ?? now.Year, month ?? now.Month, landlordId);
        return Ok(ApiResponse<CalendarMonthResponse>.Ok("Calendar retrieved", calendar));
    }

    /// <summary>
    /// The listing's tokenized public iCal feed URL (owner/admin only). Paste it into Airbnb /
    /// VRBO / Booking.com "import calendar" so stays booked here block dates there — the export
    /// half of cross-platform sync, preventing double-bookings.
    /// </summary>
    [HttpGet("{propertyId}/feed-url")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetIcalFeedUrl(string propertyId)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var path = await _calendarService.GetIcalFeedPathAsync(propertyId, landlordId, User.IsInRole("Admin"));
        var url = $"{Request.Scheme}://{Request.Host}{path}";
        return Ok(ApiResponse<object>.Ok("Calendar feed URL", new { feedUrl = url }));
    }

    /// <summary>
    /// The iCalendar document itself. Anonymous by design — external platforms poll it on a
    /// schedule — authorized by the unguessable per-property token instead of a session.
    /// </summary>
    [HttpGet("{propertyId}.ics")]
    [AllowAnonymous]
    [Produces("text/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIcalFeed(string propertyId, [FromQuery] string token = "")
    {
        var ics = await _calendarService.GetIcalFeedAsync(propertyId, token);
        return Content(ics, "text/calendar");
    }
}
