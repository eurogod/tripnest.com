using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Receipts;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptService _receiptService;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(IReceiptService receiptService, ILogger<ReceiptsController> logger)
    {
        _receiptService = receiptService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user's receipts
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReceiptResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReceiptResponse>>>> GetMyReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<PagedResult<ReceiptResponse>>.UnAuthorized());

            var receipts = await _receiptService.GetUserReceiptsAsync(userId, page, pageSize);
            return Ok(ApiResponse<PagedResult<ReceiptResponse>>.Ok("Receipts retrieved", receipts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipts");
            return StatusCode(500, ApiResponse<PagedResult<ReceiptResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Get receipt details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReceiptResponse>>> GetReceipt(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ReceiptResponse>.UnAuthorized());

        var receipt = await _receiptService.GetReceiptAsync(id, userId);
        if (receipt == null)
            return NotFound(ApiResponse<ReceiptResponse>.NotFound("Receipt"));

        return Ok(ApiResponse<ReceiptResponse>.Ok("Receipt retrieved", receipt));
    }

    /// <summary>
    /// Download receipt as PDF
    /// </summary>
    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadReceipt(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var (pdf, filename) = await _receiptService.DownloadReceiptPdfAsync(id, userId);
            if (pdf == null)
                return NotFound();

            return File(pdf, "application/pdf", filename);
        }
        catch (InvalidOperationException)
        {
            // Service throws this when the receipt doesn't exist / isn't the user's — that's a 404, not a 500.
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading receipt");
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Get receipt by booking ID
    /// </summary>
    [HttpGet("booking/{bookingId}")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReceiptResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReceiptResponse>>> GetReceiptByBooking(string bookingId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ReceiptResponse>.UnAuthorized());

        var receipt = await _receiptService.GetReceiptByBookingAsync(bookingId, userId);
        if (receipt == null)
            return NotFound(ApiResponse<ReceiptResponse>.NotFound("Receipt"));

        return Ok(ApiResponse<ReceiptResponse>.Ok("Receipt retrieved", receipt));
    }
}
