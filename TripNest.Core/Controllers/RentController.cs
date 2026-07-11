using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Rent;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

/// <summary>
/// Monthly rent for long-term stays. A qualifying booking charges only its first period upfront
/// (the escrow); the schedule here covers every later period — tenants pay month by month and
/// landlords are paid out per month, net of the platform fee.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class RentController : ControllerBase
{
    private readonly IRentService _rentService;

    public RentController(IRentService rentService) => _rentService = rentService;

    /// <summary>The caller's rent invoices across their long-term stays, soonest due first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RentInvoiceResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<RentInvoiceResponse>>>> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var invoices = await _rentService.GetMyInvoicesAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<RentInvoiceResponse>>.Ok("Rent invoices retrieved", invoices));
    }

    /// <summary>A booking's full rent schedule (its tenant or the property's landlord).</summary>
    [HttpGet("booking/{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<List<RentInvoiceResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<List<RentInvoiceResponse>>>> GetForBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var schedule = await _rentService.GetForBookingAsync(bookingId, userId);
        return Ok(ApiResponse<List<RentInvoiceResponse>>.Ok("Rent schedule retrieved", schedule));
    }

    /// <summary>Starts the tenant's checkout for one month's rent.</summary>
    [HttpPost("invoices/{invoiceId}/pay")]
    [ProducesResponseType(typeof(ApiResponse<RentInvoiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RentInvoiceResponse>>> Pay(string invoiceId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var invoice = await _rentService.InitiatePaymentAsync(invoiceId, userId);
        return Ok(ApiResponse<RentInvoiceResponse>.Ok("Rent checkout started", invoice));
    }

    /// <summary>Actively confirms a rent payment with the provider (webhook fallback).</summary>
    [HttpPost("invoices/{invoiceId}/verify")]
    [ProducesResponseType(typeof(ApiResponse<RentInvoiceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RentInvoiceResponse>>> Verify(string invoiceId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var invoice = await _rentService.VerifyPaymentAsync(invoiceId, userId);
        return Ok(ApiResponse<RentInvoiceResponse>.Ok("Rent payment verified", invoice));
    }
}
