using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Reviews;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> CreateReview([FromBody] CreateReviewRequest request)
    {
        try
        {
            var reviewerId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(reviewerId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var review = await _reviewService.CreateReviewAsync(request.BookingId, request.PropertyId, reviewerId, request.Rating, request.Comment);
            var reviewId = ((dynamic)review).Id;
            return Created($"api/reviews/{reviewId}", ApiResponse<object>.Created("Review", review));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get reviews for a property
    /// </summary>
    [HttpGet("property/{propertyId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetPropertyReviews(string propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reviews = await _reviewService.GetPropertyReviewsAsync(propertyId, page, pageSize);
            return Ok(ApiResponse<object>.Ok("Reviews retrieved", reviews));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get current user's reviews
    /// </summary>
    [HttpGet("mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyReviews()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var reviews = await _reviewService.GetUserReviewsAsync(userId);
            return Ok(ApiResponse<object>.Ok("Reviews retrieved", reviews));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get review by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetReview(string id)
    {
        try
        {
            var review = await _reviewService.GetReviewAsync(id);
            if (review == null)
                return NotFound(ApiResponse<object>.NotFound("Review"));

            return Ok(ApiResponse<object>.Ok("Review retrieved", review));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving review");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Delete own review
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteReview(string id)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _reviewService.DeleteReviewAsync(id, userId);
            return Ok(ApiResponse<object>.Ok("Review deleted", null));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting review");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
