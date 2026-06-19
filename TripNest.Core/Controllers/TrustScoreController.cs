using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.TrustScore;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TrustScoreController : ControllerBase
{
    private readonly ITrustScoreService _trustScoreService;
    private readonly ITrustScoreSnapshotRepository _snapshotRepository;
    private readonly IStayFeedbackRepository _feedbackRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<TrustScoreController> _logger;

    public TrustScoreController(
        ITrustScoreService trustScoreService,
        ITrustScoreSnapshotRepository snapshotRepository,
        IStayFeedbackRepository feedbackRepository,
        IBookingRepository bookingRepository,
        ILogger<TrustScoreController> logger)
    {
        _trustScoreService = trustScoreService;
        _snapshotRepository = snapshotRepository;
        _feedbackRepository = feedbackRepository;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    [HttpGet("property/{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<TrustScoreResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrustScoreResponse>>> GetPropertyTrustScore(string propertyId)
    {
        try
        {
            var snapshot = await _snapshotRepository.GetLatestAsync("Property", propertyId);
            if (snapshot == null)
                return NotFound(ApiResponse<TrustScoreResponse>.NotFound("Trust score"));

            var response = MapToResponse(snapshot);
            return Ok(ApiResponse<TrustScoreResponse>.Ok("Property trust score retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property trust score");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<TrustScoreResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TrustScoreResponse>>> GetUserTrustScore(string userId)
    {
        try
        {
            var snapshot = await _snapshotRepository.GetLatestAsync("User", userId);
            if (snapshot == null)
                return NotFound(ApiResponse<TrustScoreResponse>.NotFound("Trust score"));

            var response = MapToResponse(snapshot);
            return Ok(ApiResponse<TrustScoreResponse>.Ok("User trust score retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user trust score");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("stay-feedback")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> SubmitStayFeedback([FromBody] StayFeedbackRequest request)
    {
        try
        {
            var tenantId = User.GetUserId();
            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var booking = await _bookingRepository.GetByIdAsync(request.BookingId);
            if (booking == null || booking.TenantId != tenantId)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid booking"));

            var existing = await _feedbackRepository.GetByBookingIdAsync(request.BookingId);
            if (existing != null)
                return BadRequest(ApiResponse<object>.BadRequest("Feedback already submitted for this booking"));

            var feedback = new StayFeedback
            {
                BookingId = request.BookingId,
                PropertyId = booking.PropertyId,
                LandlordId = booking.Property?.UserId ?? "",
                TenantId = tenantId,
                AccuracyRating = request.AccuracyRating,
                CleanlinessRating = request.CleanlinessRating,
                SafetyRating = request.SafetyRating,
                Comment = request.Comment
            };

            await _feedbackRepository.AddAsync(feedback);
            await _feedbackRepository.SaveChangesAsync();

            await _trustScoreService.RecalculateNowAsync("Property", booking.PropertyId);
            await _trustScoreService.RecalculateNowAsync("User", booking.Property?.UserId ?? "");

            return Created($"api/trust-score/feedback/{feedback.Id}", ApiResponse<object>.Created("Feedback", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting stay feedback");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    private TrustScoreResponse MapToResponse(TrustScoreSnapshot snapshot)
    {
        var label = snapshot.FinalScore switch
        {
            >= 80 => "Highly Trusted",
            >= 60 => "Trusted",
            >= 40 => "Mixed Signals",
            _ => "Use Caution"
        };

        var trend = snapshot.Trend.ToString();

        return new TrustScoreResponse
        {
            SubjectId = snapshot.SubjectId,
            SubjectType = snapshot.SubjectType,
            FinalScore = snapshot.FinalScore,
            Trend = trend,
            Label = label,
            VerificationComponent = snapshot.VerificationComponent,
            HistoryComponent = snapshot.HistoryComponent,
            FeedbackComponent = snapshot.FeedbackComponent
        };
    }
}
