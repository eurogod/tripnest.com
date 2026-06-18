using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(IBookingService bookingService, ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        try
        {
            var tenantId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var response = await _bookingService.CreateBookingAsync(tenantId, request);

            return Created($"api/bookings/{response.BookingId}", ApiResponse<BookingResponse>.Created("Booking", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> GetBooking(string bookingId)
    {
        try
        {
            var response = await _bookingService.GetBookingAsync(bookingId);
            return Ok(ApiResponse<BookingResponse>.Ok("Booking retrieved", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving booking");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("user/my-bookings")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BookingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<BookingResponse>>>> GetUserBookings()
    {
        try
        {
            var tenantId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var response = await _bookingService.GetUserBookingsAsync(tenantId);
            return Ok(ApiResponse<IEnumerable<BookingResponse>>.Ok("User bookings retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user bookings");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("{bookingId}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CancelBooking(string bookingId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var response = await _bookingService.CancelBookingAsync(bookingId);
            return Ok(ApiResponse<BookingResponse>.Ok("Booking cancelled successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
