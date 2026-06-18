using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyReceipts([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var receipts = await _receiptService.GetUserReceiptsAsync(userId, page, pageSize);
            return Ok(ApiResponse<object>.Ok("Receipts retrieved", receipts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipts");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get receipt details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetReceipt(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var receipt = await _receiptService.GetReceiptAsync(id, userId);
            if (receipt == null)
                return NotFound(ApiResponse<object>.NotFound("Receipt"));

            return Ok(ApiResponse<object>.Ok("Receipt retrieved", receipt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipt");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
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
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var (pdf, filename) = await _receiptService.DownloadReceiptPdfAsync(id, userId);
            if (pdf == null)
                return NotFound();

            return File(pdf, "application/pdf", filename);
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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetReceiptByBooking(string bookingId)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var receipt = await _receiptService.GetReceiptByBookingAsync(bookingId, userId);
            if (receipt == null)
                return NotFound(ApiResponse<object>.NotFound("Receipt"));

            return Ok(ApiResponse<object>.Ok("Receipt retrieved", receipt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving receipt");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
