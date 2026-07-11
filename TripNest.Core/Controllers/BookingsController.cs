using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICancellationPolicyService _cancellationPolicyService;
    private readonly ISplitBillingService _splitBillingService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        IBookingService bookingService,
        ICancellationPolicyService cancellationPolicyService,
        ISplitBillingService splitBillingService,
        ILogger<BookingsController> logger)
    {
        _bookingService = bookingService;
        _cancellationPolicyService = cancellationPolicyService;
        _splitBillingService = splitBillingService;
        _logger = logger;
    }

    /// <summary>Previews the refund the tenant would get if they cancelled now (no state change).</summary>
    [HttpGet("{bookingId}/cancellation-preview")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<RefundPreview>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<RefundPreview>>> CancellationPreview(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var preview = await _cancellationPolicyService.PreviewAsync(bookingId, userId);
        return Ok(ApiResponse<RefundPreview>.Ok("Cancellation preview", preview));
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var tenantId = User.GetUserId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        // Domain failures (validation/not-found/conflict) are translated by ExceptionHandlingMiddleware.
        var response = await _bookingService.CreateBookingAsync(tenantId, request);
        return Created($"api/bookings/{response.BookingId}", ApiResponse<BookingResponse>.Created("Booking", response));
    }

    [HttpGet("{bookingId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> GetBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        // Only the booking's tenant or the property's landlord may read it.
        var response = await _bookingService.GetBookingAsync(bookingId, userId);
        return Ok(ApiResponse<BookingResponse>.Ok("Booking retrieved", response));
    }

    [HttpGet("user/my-bookings")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<BookingResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<BookingResponse>>>> GetUserBookings()
    {
        var tenantId = User.GetUserId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var response = await _bookingService.GetUserBookingsAsync(tenantId);
        return Ok(ApiResponse<IEnumerable<BookingResponse>>.Ok("User bookings retrieved", response));
    }

    [HttpPost("{bookingId}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<BookingResponse>>> CancelBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var response = await _bookingService.CancelBookingAsync(bookingId, userId);
        return Ok(ApiResponse<BookingResponse>.Ok("Booking cancelled successfully", response));
    }

    /// <summary>The group booking's shares — who owes what and who has paid (members + landlord).</summary>
    [HttpGet("{bookingId}/shares")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<BookingShareResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<BookingShareResponse>>>> GetShares(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var shares = await _splitBillingService.GetSharesAsync(bookingId, userId);
        return Ok(ApiResponse<List<BookingShareResponse>>.Ok("Booking shares retrieved", shares));
    }

    /// <summary>Starts the caller's own checkout for their share of a group booking.</summary>
    [HttpPost("shares/{shareId}/pay")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingShareResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BookingShareResponse>>> PayShare(string shareId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var share = await _splitBillingService.InitiateSharePaymentAsync(shareId, userId);
        return Ok(ApiResponse<BookingShareResponse>.Ok("Share checkout started", share));
    }

    /// <summary>Actively confirms the caller's share payment with the provider (webhook fallback).
    /// When the last share confirms, the booking confirms and the escrow holds.</summary>
    [HttpPost("shares/{shareId}/verify")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BookingShareResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<BookingShareResponse>>> VerifyShare(string shareId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var share = await _splitBillingService.VerifySharePaymentAsync(shareId, userId);
        return Ok(ApiResponse<BookingShareResponse>.Ok("Share payment verified", share));
    }
}
