using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Claims;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

/// <summary>
/// Damage-protection claims: the host files with photo evidence within the filing window after
/// checkout, the tenant can attach their side, an admin decides — and approval pays the host
/// immediately through the standard transfer machinery, fee-free.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ClaimsController : ControllerBase
{
    public record RespondRequest(string Response);
    public record ApproveRequest(decimal? ApprovedAmount, string? Note);
    public record RejectRequest(string Reason);

    private readonly IDamageClaimService _claimService;

    public ClaimsController(IDamageClaimService claimService) => _claimService = claimService;

    /// <summary>Files a claim on a booking (landlord only; multipart with evidence photos).</summary>
    [HttpPost]
    [Authorize(Roles = "Landlord,Agent,Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<DamageClaimResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DamageClaimResponse>>> File(
        [FromForm] string bookingId, [FromForm] decimal amount, [FromForm] string description,
        IFormFileCollection? photos)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var claim = await _claimService.FileAsync(userId, bookingId, amount, description, photos);
        return StatusCode(201, ApiResponse<DamageClaimResponse>.Created("Damage claim", claim));
    }

    /// <summary>The caller's filed claims (as landlord), newest first.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DamageClaimResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DamageClaimResponse>>>> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var claims = await _claimService.GetMineAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<DamageClaimResponse>>.Ok("Claims retrieved", claims));
    }

    /// <summary>The claim on a booking — its landlord, tenant, or an admin.</summary>
    [HttpGet("booking/{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<DamageClaimResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<DamageClaimResponse>>> GetForBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var claim = await _claimService.GetForBookingAsync(bookingId, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<DamageClaimResponse>.Ok("Claim retrieved", claim));
    }

    /// <summary>The tenant attaches their single response before the admin decides.</summary>
    [HttpPost("{id}/respond")]
    [ProducesResponseType(typeof(ApiResponse<DamageClaimResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DamageClaimResponse>>> Respond(string id, [FromBody] RespondRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var claim = await _claimService.RespondAsync(id, userId, request.Response);
        return Ok(ApiResponse<DamageClaimResponse>.Ok("Response recorded", claim));
    }

    // ------------------------------------------------------------------ admin

    /// <summary>Claims awaiting a decision, oldest first (admin).</summary>
    [HttpGet("review")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DamageClaimResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<DamageClaimResponse>>>> GetForReview(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var claims = await _claimService.GetForReviewAsync(page, pageSize);
        return Ok(ApiResponse<PagedResult<DamageClaimResponse>>.Ok("Claims for review", claims));
    }

    /// <summary>Approves the claim (optionally at a reduced amount) — pays the host now.</summary>
    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DamageClaimResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DamageClaimResponse>>> Approve(string id, [FromBody] ApproveRequest request)
    {
        var claim = await _claimService.ApproveAsync(id, request.ApprovedAmount, request.Note);
        return Ok(ApiResponse<DamageClaimResponse>.Ok("Claim approved and payout initiated", claim));
    }

    /// <summary>Rejects the claim with a reason (admin).</summary>
    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<DamageClaimResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<DamageClaimResponse>>> Reject(string id, [FromBody] RejectRequest request)
    {
        var claim = await _claimService.RejectAsync(id, request.Reason);
        return Ok(ApiResponse<DamageClaimResponse>.Ok("Claim rejected", claim));
    }
}
