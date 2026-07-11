using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Payouts;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

/// <summary>
/// Host disbursements: where the money goes (payout account) and the payouts themselves.
/// Escrow releases create payouts automatically; hosts manage the destination and can retry
/// failed transfers here. Domain failures are translated by ExceptionHandlingMiddleware.
/// </summary>
[ApiController]
[Route("api/payouts")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Agent,Admin")]
public class PayoutsController : ControllerBase
{
    private readonly IPayoutService _payoutService;

    public PayoutsController(IPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    /// <summary>The caller's payout account (account number masked), 404 until registered.</summary>
    [HttpGet("account")]
    [ProducesResponseType(typeof(ApiResponse<PayoutAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayoutAccountResponse>>> GetAccount()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PayoutAccountResponse>.UnAuthorized());

        var account = await _payoutService.GetMyAccountAsync(userId);
        if (account is null)
            return NotFound(ApiResponse<PayoutAccountResponse>.NotFound("Payout account"));

        return Ok(ApiResponse<PayoutAccountResponse>.Ok("Payout account retrieved", account));
    }

    /// <summary>
    /// Registers/replaces the caller's payout destination (Mobile Money wallet or bank account).
    /// The account is registered with Paystack as a transfer recipient before being saved.
    /// </summary>
    [HttpPut("account")]
    [ProducesResponseType(typeof(ApiResponse<PayoutAccountResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PayoutAccountResponse>>> UpsertAccount([FromBody] UpsertPayoutAccountRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PayoutAccountResponse>.UnAuthorized());

        var account = await _payoutService.UpsertMyAccountAsync(userId, request);
        return Ok(ApiResponse<PayoutAccountResponse>.Ok("Payout account saved", account));
    }

    /// <summary>The caller's payouts, newest first — the earnings "money actually sent" view.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PayoutResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<PayoutResponse>>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<PayoutResponse>>.UnAuthorized());

        var payouts = await _payoutService.GetMyPayoutsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<PayoutResponse>>.Ok("Payouts retrieved", payouts));
    }

    /// <summary>Re-attempts a Pending or Failed payout (e.g. after fixing the payout account).</summary>
    [HttpPost("{id}/retry")]
    [ProducesResponseType(typeof(ApiResponse<PayoutResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PayoutResponse>>> Retry(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PayoutResponse>.UnAuthorized());

        var payout = await _payoutService.RetryAsync(id, userId);
        return Ok(ApiResponse<PayoutResponse>.Ok("Payout retried", payout));
    }
}
