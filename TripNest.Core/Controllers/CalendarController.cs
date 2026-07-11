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
    private readonly IExternalCalendarService _externalCalendarService;

    public CalendarController(ICalendarService calendarService, IExternalCalendarService externalCalendarService)
    {
        _calendarService = calendarService;
        _externalCalendarService = externalCalendarService;
    }

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

    /// <summary>
    /// Links an external iCal feed (Airbnb/VRBO/Booking.com export URL) to the listing and runs
    /// the first import immediately — the import half of cross-platform sync. Imported busy
    /// ranges appear as blocked dates, so double-bookings are prevented at source.
    /// </summary>
    [HttpPost("{propertyId}/external")]
    [ProducesResponseType(typeof(ApiResponse<ExternalCalendarResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ExternalCalendarResponse>>> AddExternalCalendar(
        string propertyId, [FromBody] AddExternalCalendarRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var isAdmin = User.IsInRole("Admin");
        var calendar = await _externalCalendarService.AddAsync(propertyId, request.Name, request.FeedUrl, userId, isAdmin);
        calendar = await _externalCalendarService.SyncAsync(calendar.Id, userId, isAdmin);
        return StatusCode(201, ApiResponse<ExternalCalendarResponse>.Created("External calendar", calendar));
    }

    /// <summary>The listing's linked external calendars with sync status.</summary>
    [HttpGet("{propertyId}/external")]
    [ProducesResponseType(typeof(ApiResponse<List<ExternalCalendarResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ExternalCalendarResponse>>>> GetExternalCalendars(string propertyId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var calendars = await _externalCalendarService.GetForPropertyAsync(propertyId, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<List<ExternalCalendarResponse>>.Ok("External calendars retrieved", calendars));
    }

    /// <summary>Re-imports one linked feed on demand (the worker also does this periodically).</summary>
    [HttpPost("external/{id}/sync")]
    [ProducesResponseType(typeof(ApiResponse<ExternalCalendarResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ExternalCalendarResponse>>> SyncExternalCalendar(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var calendar = await _externalCalendarService.SyncAsync(id, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<ExternalCalendarResponse>.Ok("Calendar synced", calendar));
    }

    /// <summary>Unlinks a feed and removes the blocked dates it imported (manual blocks stay).</summary>
    [HttpDelete("external/{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveExternalCalendar(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _externalCalendarService.RemoveAsync(id, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<object>.Ok("External calendar removed", new { }));
    }
}
