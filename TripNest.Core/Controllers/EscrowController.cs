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

        // Paystack signs the raw body with HMAC-SHA512 of your secret key. Without a configured
        // secret we cannot authenticate the webhook, so reject (never process an unverifiable call).
        var secret = _configuration["PaystackSettings:SecretKey"];
        var providedSignature = Request.Headers["x-paystack-signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(secret) || !IsPaystackSignatureValid(rawBody, secret, providedSignature))
        {
            _logger.LogWarning("Rejected escrow webhook with invalid/missing signature or unconfigured key");
            return Unauthorized(ApiResponse<object>.UnAuthorized());
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var eventType = root.TryGetProperty("event", out var ev) ? ev.GetString() : null;

            // Only a successful charge moves funds into escrow; ignore other events.
            if (eventType != "charge.success")
                return Ok(ApiResponse<object>.Ok("Event ignored", null));

            var data = root.GetProperty("data");
            var reference = data.TryGetProperty("reference", out var r) ? r.GetString() : null;
            var bookingId = data.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("bookingId", out var b)
                ? b.GetString() : null;

            if (string.IsNullOrEmpty(bookingId) || string.IsNullOrEmpty(reference))
                return BadRequest(ApiResponse<object>.BadRequest("Missing booking or payment reference"));

            // Paystack reports the charged amount in the minor unit (pesewas); convert to GHS so the
            // service can verify it matches what the booking is owed before holding the funds.
            var paidAmount = data.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var minor)
                ? minor / 100m
                : 0m;

            await _escrowService.VerifyAndHoldPaymentAsync(bookingId, reference, paidAmount);
            return Ok(ApiResponse<object>.Ok("Payment verified", null));
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Malformed webhook payload"));
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
    /// Verifies Paystack's HMAC-SHA512 signature (hex-encoded) over the raw body, in constant time.
    /// </summary>
    private static bool IsPaystackSignatureValid(string rawBody, string secret, string? providedSignature)
    {
        if (string.IsNullOrEmpty(providedSignature))
            return false;

        var computed = Convert.ToHexString(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant()));
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
