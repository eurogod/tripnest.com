using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
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
    [ProducesResponseType(typeof(ApiResponse<List<LandlordBookingResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<LandlordBookingResponse>>>> GetBookings()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<List<LandlordBookingResponse>>.UnAuthorized());

        var bookings = await _workspaceService.GetBookingsAsync(landlordId);
        return Ok(ApiResponse<List<LandlordBookingResponse>>.Ok("Bookings retrieved", bookings));
    }

    /// <summary>The caller's tenant roster, derived from active bookings.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(ApiResponse<List<LandlordTenantResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<LandlordTenantResponse>>>> GetTenants()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<List<LandlordTenantResponse>>.UnAuthorized());

        var tenants = await _workspaceService.GetTenantsAsync(landlordId);
        return Ok(ApiResponse<List<LandlordTenantResponse>>.Ok("Tenants retrieved", tenants));
    }

    /// <summary>Pre-booking enquiries sent to the caller's listings.</summary>
    [HttpGet("inquiries")]
    [ProducesResponseType(typeof(ApiResponse<List<InquiryResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<InquiryResponse>>>> GetInquiries()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<List<InquiryResponse>>.UnAuthorized());

        var inquiries = await _inquiryService.GetForLandlordAsync(landlordId);
        return Ok(ApiResponse<List<InquiryResponse>>.Ok("Inquiries retrieved", inquiries));
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
