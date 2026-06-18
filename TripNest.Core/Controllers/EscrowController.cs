using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class EscrowController : ControllerBase
{
    private readonly IEscrowService _escrowService;
    private readonly ILogger<EscrowController> _logger;

    public EscrowController(IEscrowService escrowService, ILogger<EscrowController> logger)
    {
        _escrowService = escrowService;
        _logger = logger;
    }

    /// <summary>
    /// Initiate escrow payment for a booking
    /// </summary>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> InitiatePayment([FromBody] InitiateEscrowRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var result = await _escrowService.InitiatePaymentAsync(request.BookingId, request.Amount);
            return Ok(ApiResponse<object>.Ok("Payment initiated", result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating payment");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Handle payment provider webhook callback
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> HandleWebhook([FromBody] WebhookCallbackRequest request)
    {
        try
        {
            await _escrowService.VerifyAndHoldPaymentAsync(request.BookingId, request.Reference);
            return Ok(ApiResponse<object>.Ok("Payment verified", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get escrow transaction details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetEscrow(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var escrow = await _escrowService.GetEscrowAsync(id, userId);
            if (escrow == null)
                return NotFound(ApiResponse<object>.NotFound("Escrow"));

            return Ok(ApiResponse<object>.Ok("Escrow retrieved", escrow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving escrow");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Release held escrow funds
    /// </summary>
    [HttpPost("{id}/release")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ReleaseEscrow(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _escrowService.ReleaseEscrowAsync(id, userId);
            return Ok(ApiResponse<object>.Ok("Escrow released", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing escrow");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Flag escrow dispute
    /// </summary>
    [HttpPost("{id}/dispute")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> DisputeEscrow(string id, [FromBody] DisputeRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _escrowService.RaiseDisputeAsync(id, userId, request.Reason);
            return Ok(ApiResponse<object>.Ok("Dispute raised", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising dispute");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Resolve escrow dispute (Admin only)
    /// </summary>
    [HttpPatch("{id}/resolve-dispute")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ResolveDispute(string id, [FromBody] ResolveDisputeRequest request)
    {
        try
        {
            await _escrowService.ResolveDisputeAsync(id, request.Approved);
            return Ok(ApiResponse<object>.Ok("Dispute resolved", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving dispute");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Refund escrow amount
    /// </summary>
    [HttpPost("{id}/refund")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RefundEscrow(string id, [FromBody] RefundRequest request)
    {
        try
        {
            await _escrowService.RefundEscrowAsync(id, request.Reason);
            return Ok(ApiResponse<object>.Ok("Escrow refunded", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding escrow");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
