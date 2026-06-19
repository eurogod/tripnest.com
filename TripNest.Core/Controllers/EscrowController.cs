using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Extensions;
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
    private readonly IConfiguration _configuration;
    private readonly ILogger<EscrowController> _logger;

    public EscrowController(IEscrowService escrowService, IConfiguration configuration, ILogger<EscrowController> logger)
    {
        _escrowService = escrowService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initiate escrow payment for a booking
    /// </summary>
    [HttpPost("initiate")]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<EscrowResponse>>> InitiatePayment([FromBody] InitiateEscrowRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

            // Amount is derived from the booking server-side, not taken from the client.
            var result = await _escrowService.InitiatePaymentAsync(request.BookingId, userId);
            return Ok(ApiResponse<EscrowResponse>.Ok("Payment initiated", result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<EscrowResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating payment");
            return StatusCode(500, ApiResponse<EscrowResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Handle payment provider webhook callback
    /// </summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> HandleWebhook()
    {
        // Read the raw body so the signature is verified over the exact bytes the
        // provider signed, before we trust anything inside it.
        string rawBody;
        Request.EnableBuffering();
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        var secret = _configuration["Escrow:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogError("Escrow:WebhookSecret is not configured — rejecting webhook");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }

        var providedSignature = Request.Headers["X-Signature"].FirstOrDefault();
        if (!IsSignatureValid(rawBody, secret, providedSignature))
        {
            _logger.LogWarning("Rejected escrow webhook with invalid or missing signature");
            return Unauthorized(ApiResponse<object>.UnAuthorized());
        }

        WebhookCallbackRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<WebhookCallbackRequest>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Malformed webhook payload"));
        }

        if (request == null || string.IsNullOrEmpty(request.BookingId) || string.IsNullOrEmpty(request.Reference))
            return BadRequest(ApiResponse<object>.BadRequest("Missing booking or payment reference"));

        try
        {
            await _escrowService.VerifyAndHoldPaymentAsync(request.BookingId, request.Reference);
            return Ok(ApiResponse<object>.Ok("Payment verified", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling webhook");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Verifies an HMAC-SHA256 signature (hex-encoded) of the raw body using the shared secret,
    /// in constant time to avoid timing attacks.
    /// </summary>
    private static bool IsSignatureValid(string rawBody, string secret, string? providedSignature)
    {
        if (string.IsNullOrEmpty(providedSignature))
            return false;

        var computed = Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(providedSignature.Trim().ToUpperInvariant()));
    }

    /// <summary>
    /// Get escrow transaction details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EscrowResponse>>> GetEscrow(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

            var escrow = await _escrowService.GetEscrowAsync(id, userId);
            if (escrow == null)
                return NotFound(ApiResponse<EscrowResponse>.NotFound("Escrow"));

            return Ok(ApiResponse<EscrowResponse>.Ok("Escrow retrieved", escrow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving escrow");
            return StatusCode(500, ApiResponse<EscrowResponse>.InternalServerError());
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
            var userId = User.GetUserId();
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
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _escrowService.RaiseDisputeAsync(id, userId, request.Reason);
            return Ok(ApiResponse<object>.Ok("Dispute raised", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding escrow");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
