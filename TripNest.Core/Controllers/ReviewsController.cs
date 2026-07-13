using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Reviews;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviewService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    /// <summary>
    /// Submit a review for a completed booking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ReviewResponse>>> CreateReview([FromBody] CreateReviewRequest request)
    {
        var reviewerId = User.GetUserId();
        if (string.IsNullOrEmpty(reviewerId))
            return Unauthorized(ApiResponse<ReviewResponse>.UnAuthorized());

        var review = await _reviewService.CreateReviewAsync(request.BookingId, request.PropertyId, reviewerId, request.Rating, request.Comment);
        return Created($"api/reviews/{review.ReviewId}", ApiResponse<ReviewResponse>.Created("Review", review));
    }

    /// <summary>
    /// Get reviews for a property
    /// </summary>
    [HttpGet("property/{propertyId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReviewResponse>>>> GetPropertyReviews(string propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var reviews = await _reviewService.GetPropertyReviewsAsync(propertyId, page, pageSize);
        return Ok(ApiResponse<PagedResult<ReviewResponse>>.Ok("Reviews retrieved", reviews));
    }

    /// <summary>
    /// Get current user's reviews
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReviewResponse>>>> GetMyReviews([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<ReviewResponse>>.UnAuthorized());

        var reviews = await _reviewService.GetUserReviewsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<ReviewResponse>>.Ok("Reviews retrieved", reviews));
    }

    /// <summary>
    /// Get review by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<ReviewResponse>>> GetReview(string id)
    {
        var review = await _reviewService.GetReviewAsync(id);
        if (review == null)
            return NotFound(ApiResponse<ReviewResponse>.NotFound("Review"));

        return Ok(ApiResponse<ReviewResponse>.Ok("Review retrieved", review));
    }

    /// <summary>
    /// Delete own review
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<ReviewResponse>>> DeleteReview(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ReviewResponse>.UnAuthorized());

        // NotFoundException → 404 and ForbiddenException → 403 via the middleware (the old catch
        // reported a missing review as 403).
        await _reviewService.DeleteReviewAsync(id, userId);
        return Ok(ApiResponse<ReviewResponse>.Ok("Review deleted", null));
    }

    /// <summary>AI "what guests say" for a listing — themes from its reviews (cached ~24h; needs 2+ reviews).</summary>
    [HttpGet("property/{propertyId}/summary")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TripNest.Core.DTOs.Ai.ReviewSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TripNest.Core.DTOs.Ai.ReviewSummaryResponse>>> GetReviewSummary(
        string propertyId, [FromServices] IAiInsightsService aiInsights)
    {
        var summary = await aiInsights.GetReviewSummaryAsync(propertyId);
        return Ok(ApiResponse<TripNest.Core.DTOs.Ai.ReviewSummaryResponse>.Ok("Review summary", summary));
    }
}
