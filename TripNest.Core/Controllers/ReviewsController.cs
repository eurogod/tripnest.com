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
        try
        {
            var reviewerId = User.GetUserId();
            if (string.IsNullOrEmpty(reviewerId))
                return Unauthorized(ApiResponse<ReviewResponse>.UnAuthorized());

            var review = await _reviewService.CreateReviewAsync(request.BookingId, request.PropertyId, reviewerId, request.Rating, request.Comment);
            return Created($"api/reviews/{review.ReviewId}", ApiResponse<ReviewResponse>.Created("Review", review));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ReviewResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, ApiResponse<ReviewResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Get reviews for a property
    /// </summary>
    [HttpGet("property/{propertyId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ReviewResponse>>>> GetPropertyReviews(string propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reviews = await _reviewService.GetPropertyReviewsAsync(propertyId, page, pageSize);
            return Ok(ApiResponse<PagedResult<ReviewResponse>>.Ok("Reviews retrieved", reviews));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews");
            return StatusCode(500, ApiResponse<PagedResult<ReviewResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Get current user's reviews
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ReviewResponse>>>> GetMyReviews()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<List<ReviewResponse>>.UnAuthorized());

            var reviews = await _reviewService.GetUserReviewsAsync(userId);
            return Ok(ApiResponse<List<ReviewResponse>>.Ok("Reviews retrieved", reviews));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews");
            return StatusCode(500, ApiResponse<List<ReviewResponse>>.InternalServerError());
        }
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
        try
        {
            var review = await _reviewService.GetReviewAsync(id);
            if (review == null)
                return NotFound(ApiResponse<ReviewResponse>.NotFound("Review"));

            return Ok(ApiResponse<ReviewResponse>.Ok("Review retrieved", review));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review");
            return StatusCode(500, ApiResponse<ReviewResponse>.InternalServerError());
        }
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
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ReviewResponse>.UnAuthorized());

            await _reviewService.DeleteReviewAsync(id, userId);
            return Ok(ApiResponse<ReviewResponse>.Ok("Review deleted", null));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, ApiResponse<ReviewResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review");
            return StatusCode(500, ApiResponse<ReviewResponse>.InternalServerError());
        }
    }
}
