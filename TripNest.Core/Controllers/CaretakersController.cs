using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CaretakersController : ControllerBase
{
    private readonly ICaretakerService _caretakerService;
    private readonly ILogger<CaretakersController> _logger;

    public CaretakersController(ICaretakerService caretakerService, ILogger<CaretakersController> logger)
    {
        _caretakerService = caretakerService;
        _logger = logger;
    }

    /// <summary>
    /// Get available caretakers, optionally filtered by service type and area
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetCaretakers([FromQuery] string? serviceType, [FromQuery] string? area)
    {
        try
        {
            var caretakers = await _caretakerService.GetAvailableCaretakersAsync(serviceType, area);
            return Ok(ApiResponse<object>.Ok("Caretakers retrieved", caretakers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretakers");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get caretaker profile with ratings
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetCaretaker(string id)
    {
        try
        {
            var caretaker = await _caretakerService.GetCaretakerProfileAsync(id);
            if (caretaker == null)
                return NotFound(ApiResponse<object>.NotFound("Caretaker"));

            return Ok(ApiResponse<object>.Ok("Caretaker retrieved", caretaker));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretaker");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Assign caretaker to property (Landlord only)
    /// </summary>
    [HttpPost("assign")]
    [Authorize(Roles = "Landlord")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> AssignCaretaker([FromBody] AssignCaretakerRequest request)
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _caretakerService.AssignCaretakerToPropertyAsync(request.PropertyId, request.CaretakerId, landlordId);
            return Ok(ApiResponse<object>.Ok("Caretaker assigned", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning caretaker");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Create service request
    /// </summary>
    [HttpPost("service-requests")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> CreateServiceRequest([FromBody] CreateServiceRequestRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var serviceRequest = await _caretakerService.CreateServiceRequestAsync(request, userId);
            return StatusCode(201, ApiResponse<object>.Created("Service Request", serviceRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service request");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get service requests (role-aware)
    /// </summary>
    [HttpGet("service-requests/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyServiceRequests()
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var requests = await _caretakerService.GetServiceRequestsAsync(userId);
            return Ok(ApiResponse<object>.Ok("Service requests retrieved", requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service requests");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Accept service request
    /// </summary>
    [HttpPatch("service-requests/{id}/accept")]
    [Authorize(Roles = "Caretaker")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> AcceptServiceRequest(string id)
    {
        try
        {
            var caretakerId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(caretakerId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _caretakerService.AcceptServiceRequestAsync(id, caretakerId);
            return Ok(ApiResponse<object>.Ok("Service request accepted", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting service request");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Update service request status
    /// </summary>
    [HttpPatch("service-requests/{id}/status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateServiceRequestStatus(string id, [FromBody] UpdateServiceRequestStatusRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _caretakerService.UpdateServiceRequestStatusAsync(id, request.Status, userId);
            return Ok(ApiResponse<object>.Ok("Status updated", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Submit review for completed service
    /// </summary>
    [HttpPost("service-requests/{id}/review")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<object>>> ReviewService(string id, [FromBody] SubmitServiceReviewRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _caretakerService.SubmitServiceReviewAsync(id, userId, request.Rating, request.Comment);
            return StatusCode(201, ApiResponse<object>.Ok("Review submitted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting review");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
