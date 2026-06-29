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
}
