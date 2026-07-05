using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/properties")]
[Produces("application/json")]
public class WalkthroughsController : ControllerBase
{
    private readonly IWalkthroughService _walkthroughService;
    private readonly ILogger<WalkthroughsController> _logger;

    public WalkthroughsController(IWalkthroughService walkthroughService, ILogger<WalkthroughsController> logger)
    {
        _walkthroughService = walkthroughService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a walkthrough video for a property (Landlord only).
    /// Accepts multipart/form-data — use the file picker in Swagger, not a URL string.
    /// Sets WalkthroughStatus = PendingReview. Property cannot go Active until an
    /// Agent/Admin approves this video via PATCH .../walkthrough/review.
    /// </summary>
    [HttpPost("{propertyId}/walkthrough")]
    [Authorize(Roles = "Landlord")]
    [RequireVerified]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)] // 500 MB
    [ProducesResponseType(typeof(ApiResponse<WalkthroughResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<WalkthroughResponse>>> UploadWalkthrough(
        string propertyId,
        [FromForm] string title,
        IFormFile videoFile)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (videoFile == null || videoFile.Length == 0)
            return BadRequest(ApiResponse<object>.BadRequest("No video file was provided"));

        var response = await _walkthroughService.UploadWalkthroughAsync(propertyId, landlordId, title, videoFile);
        return StatusCode(201, ApiResponse<WalkthroughResponse>.Created("Walkthrough", response));
    }

    /// <summary>
    /// Approve or reject a property's walkthrough video (Agent or Admin only).
    /// Approval unlocks the property to be set Active. Rejection requires a reason
    /// which is stored and visible to the landlord.
    /// </summary>
    [HttpPatch("{propertyId}/walkthrough/review")]
    [Authorize(Roles = "Agent,Admin")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<PropertyWalkthroughStatusResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PropertyWalkthroughStatusResponse>>> ReviewWalkthrough(
        string propertyId,
        [FromBody] ReviewWalkthroughRequest request)
    {
        var reviewerId = User.GetUserId();
        if (string.IsNullOrEmpty(reviewerId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectionReason))
            return BadRequest(ApiResponse<object>.BadRequest("A rejection reason is required when declining a walkthrough"));

        var response = await _walkthroughService.ReviewWalkthroughAsync(propertyId, reviewerId, request.Approved, request.RejectionReason);
        return Ok(ApiResponse<PropertyWalkthroughStatusResponse>.Ok(
            request.Approved ? "Walkthrough approved — property can now be set to Active" : "Walkthrough rejected",
            response));
    }

    /// <summary>
    /// Get all properties with walkthroughs awaiting review (Agent or Admin only).
    /// </summary>
    [HttpGet("pending-walkthroughs")]
    [Authorize(Roles = "Agent,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyWalkthroughStatusResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyWalkthroughStatusResponse>>>> GetPendingWalkthroughs()
    {
        var pending = await _walkthroughService.GetPendingWalkthroughsAsync();
        return Ok(ApiResponse<IEnumerable<PropertyWalkthroughStatusResponse>>.Ok("Pending walkthroughs retrieved", pending));
    }

    /// <summary>Get all walkthrough videos for a property.</summary>
    [HttpGet("{propertyId}/walkthroughs")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WalkthroughResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<WalkthroughResponse>>>> GetPropertyWalkthroughs(string propertyId)
    {
        var response = await _walkthroughService.GetPropertyWalkthroughsAsync(propertyId);
        return Ok(ApiResponse<IEnumerable<WalkthroughResponse>>.Ok("Walkthroughs retrieved", response));
    }

    /// <summary>Get a single walkthrough video record.</summary>
    [HttpGet("{propertyId}/walkthroughs/{walkthroughId}")]
    [ProducesResponseType(typeof(ApiResponse<WalkthroughResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<WalkthroughResponse>>> GetWalkthrough(string propertyId, string walkthroughId)
    {
        try
        {
            var response = await _walkthroughService.GetWalkthroughAsync(walkthroughId);
            return Ok(ApiResponse<WalkthroughResponse>.Ok("Walkthrough retrieved", response));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ApiResponse<object>.NotFound("Walkthrough"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving walkthrough");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>Delete a walkthrough video (Landlord or Admin only).</summary>
    [HttpDelete("{propertyId}/walkthroughs/{walkthroughId}")]
    [Authorize(Roles = "Landlord,Admin")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWalkthrough(string propertyId, string walkthroughId)
    {
        await _walkthroughService.DeleteWalkthroughAsync(walkthroughId);
        return Ok(ApiResponse<object>.Ok("Walkthrough deleted successfully"));
    }
}
