using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Exceptions;
using TripNest.Core.Services;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class EscrowController : ControllerBase
{
    private readonly IEscrowService _escrowService;
    private readonly IPayoutService _payoutService;
    private readonly ISplitBillingService _splitBillingService;
    private readonly IRentService _rentService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EscrowController> _logger;

    public EscrowController(
        IEscrowService escrowService,
        IPayoutService payoutService,
        ISplitBillingService splitBillingService,
        IRentService rentService,
        IConfiguration configuration,
        ILogger<EscrowController> logger)
    {
        _escrowService = escrowService;
        _payoutService = payoutService;
        _splitBillingService = splitBillingService;
        _rentService = rentService;
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
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

        // Amount is derived from the booking server-side, not taken from the client.
        var result = await _escrowService.InitiatePaymentAsync(request.BookingId, userId);
        return Ok(ApiResponse<EscrowResponse>.Ok("Payment initiated", result));
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

            // Transfer lifecycle events drive payouts to Paid/Failed. The transfer reference is
            // the payout id; same HMAC signature verified above covers these events too.
            if (eventType is "transfer.success" or "transfer.failed" or "transfer.reversed")
            {
                var transferData = root.GetProperty("data");
                var transferRef = transferData.TryGetProperty("reference", out var tr) ? tr.GetString() : null;
                var failure = transferData.TryGetProperty("reason", out var rs) ? rs.GetString() : null;

                if (!string.IsNullOrEmpty(transferRef))
                    await _payoutService.HandleTransferWebhookAsync(eventType, transferRef, failure);

                return Ok(ApiResponse<object>.Ok("Transfer event processed", null));
            }

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

            // Split-billing charges carry "share:{shareId}" instead of a booking id — they pay
            // one member's slice, and the escrow holds only when the whole group has paid.
            if (bookingId.StartsWith(ISplitBillingService.ReferencePrefix, StringComparison.Ordinal))
            {
                var shareId = bookingId[ISplitBillingService.ReferencePrefix.Length..];
                await _splitBillingService.ApplySharePaymentAsync(shareId, reference, paidAmount);
                return Ok(ApiResponse<object>.Ok("Share payment recorded", null));
            }

            // Monthly-rent charges carry "rent:{invoiceId}" — they pay one period of a long-term
            // stay and disburse to the landlord immediately (no escrow hold).
            if (bookingId.StartsWith(IRentService.ReferencePrefix, StringComparison.Ordinal))
            {
                var invoiceId = bookingId[IRentService.ReferencePrefix.Length..];
                await _rentService.ApplyRentPaymentAsync(invoiceId, reference, paidAmount);
                return Ok(ApiResponse<object>.Ok("Rent payment recorded", null));
            }

            await _escrowService.VerifyAndHoldPaymentAsync(bookingId, reference, paidAmount);
            return Ok(ApiResponse<object>.Ok("Payment verified", null));
        }
        catch (JsonException)
        {
            return BadRequest(ApiResponse<object>.BadRequest("Malformed webhook payload"));
        }
        catch (ConflictException)
        {
            // Double-booking race, fully handled by the service (payment refunded or parked in
            // Disputed). Ack with 200 so the provider does not keep retrying a resolved event.
            return Ok(ApiResponse<object>.Ok("Payment could not be applied (dates already booked); resolution recorded", null));
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
    /// Actively verify a booking's payment with the provider and hold the funds if it succeeded.
    /// A reliable fallback to the webhook — call it on the post-checkout redirect, or to reconcile a
    /// booking whose webhook was missed. Idempotent: if the escrow is already held, it just returns it.
    /// </summary>
    [HttpPost("booking/{bookingId}/verify")]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<EscrowResponse>>> VerifyPayment(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

        var result = await _escrowService.VerifyPaymentByBookingAsync(bookingId, userId);
        return Ok(ApiResponse<EscrowResponse>.Ok("Payment verified", result));
    }

    /// <summary>
    /// List the caller's own escrows (as the paying tenant), for the payments "held funds" view.
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<EscrowResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<EscrowResponse>>>> GetMyEscrows([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<EscrowResponse>>.UnAuthorized());

        var escrows = await _escrowService.GetMyEscrowsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<EscrowResponse>>.Ok("Escrows retrieved", escrows));
    }

    /// <summary>
    /// Get escrow transaction details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EscrowResponse>>> GetEscrow(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

        var escrow = await _escrowService.GetEscrowAsync(id, userId);
        if (escrow == null)
            return NotFound(ApiResponse<EscrowResponse>.NotFound("Escrow"));

        return Ok(ApiResponse<EscrowResponse>.Ok("Escrow retrieved", escrow));
    }

    /// <summary>
    /// Get the escrow attached to a booking (tenant or property owner only)
    /// </summary>
    [HttpGet("booking/{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EscrowResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<EscrowResponse>>> GetEscrowByBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<EscrowResponse>.UnAuthorized());

        var escrow = await _escrowService.GetEscrowByBookingAsync(bookingId, userId);
        if (escrow == null)
            return NotFound(ApiResponse<EscrowResponse>.NotFound("Escrow"));

        return Ok(ApiResponse<EscrowResponse>.Ok("Escrow retrieved", escrow));
    }

    /// <summary>
    /// Release held escrow funds
    /// </summary>
    [HttpPost("{id}/release")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ReleaseEscrow(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _escrowService.ReleaseEscrowAsync(id, userId);
        return Ok(ApiResponse<object>.Ok("Escrow released", null));
    }

    /// <summary>
    /// Flag escrow dispute
    /// </summary>
    [HttpPost("{id}/dispute")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> DisputeEscrow(string id, [FromBody] DisputeRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _escrowService.RaiseDisputeAsync(id, userId, request.Reason);
        return Ok(ApiResponse<object>.Ok("Dispute raised", null));
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
        await _escrowService.ResolveDisputeAsync(id, request.Approved);
        return Ok(ApiResponse<object>.Ok("Dispute resolved", null));
    }

    /// <summary>
    /// Refund escrow amount
    /// </summary>
    [HttpPost("{id}/refund")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> RefundEscrow(string id, [FromBody] RefundRequest request)
    {
        await _escrowService.RefundEscrowAsync(id, request.Reason);
        return Ok(ApiResponse<object>.Ok("Escrow refunded", null));
    }
}
