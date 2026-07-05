using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

/// <summary>
/// Landlord-side workspace lists that sit alongside <see cref="LandlordDashboardController"/>:
/// incoming bookings, the tenant roster, and pre-booking enquiries.
/// </summary>
[ApiController]
[Route("api/landlord")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class LandlordWorkspaceController : ControllerBase
{
    private readonly ILandlordWorkspaceService _workspaceService;
    private readonly IInquiryService _inquiryService;

    public LandlordWorkspaceController(ILandlordWorkspaceService workspaceService, IInquiryService inquiryService)
    {
        _workspaceService = workspaceService;
        _inquiryService = inquiryService;
    }

    /// <summary>Incoming bookings across the caller's listings.</summary>
    [HttpGet("bookings")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LandlordBookingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<LandlordBookingResponse>>>> GetBookings([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PagedResult<LandlordBookingResponse>>.UnAuthorized());

        var bookings = await _workspaceService.GetBookingsAsync(landlordId, page, pageSize);
        return Ok(ApiResponse<PagedResult<LandlordBookingResponse>>.Ok("Bookings retrieved", bookings));
    }

    /// <summary>
    /// One reservation's details for the host: trip facts, guest, earnings breakdown
    /// (nightly rate, management fee, owner payout), and the guest's reviews of the listing.
    /// </summary>
    [HttpGet("reservations/{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<ReservationDetailsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReservationDetailsResponse>>> GetReservation(string bookingId)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<ReservationDetailsResponse>.UnAuthorized());

        var reservation = await _workspaceService.GetReservationAsync(bookingId, landlordId);
        return Ok(ApiResponse<ReservationDetailsResponse>.Ok("Reservation retrieved", reservation));
    }

    /// <summary>The caller's tenant roster, derived from active bookings.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LandlordTenantResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<LandlordTenantResponse>>>> GetTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PagedResult<LandlordTenantResponse>>.UnAuthorized());

        var tenants = await _workspaceService.GetTenantsAsync(landlordId, page, pageSize);
        return Ok(ApiResponse<PagedResult<LandlordTenantResponse>>.Ok("Tenants retrieved", tenants));
    }

    /// <summary>Pre-booking enquiries sent to the caller's listings.</summary>
    [HttpGet("inquiries")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InquiryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<InquiryResponse>>>> GetInquiries([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PagedResult<InquiryResponse>>.UnAuthorized());

        var inquiries = await _inquiryService.GetForLandlordAsync(landlordId, page, pageSize);
        return Ok(ApiResponse<PagedResult<InquiryResponse>>.Ok("Inquiries retrieved", inquiries));
    }

    /// <summary>Update the status of an enquiry (new / replied / archived).</summary>
    [HttpPatch("inquiries/{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<InquiryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<InquiryResponse>>> UpdateInquiryStatus(string id, [FromBody] UpdateInquiryStatusRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<InquiryResponse>.UnAuthorized());

        var inquiry = await _inquiryService.UpdateStatusAsync(id, request.Status, landlordId);
        return Ok(ApiResponse<InquiryResponse>.Ok("Inquiry updated", inquiry));
    }
}
