using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;

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
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<List<CaretakerResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<CaretakerResponse>>>> GetCaretakers([FromQuery] string? serviceType, [FromQuery] string? area)
    {
        try
        {
            var caretakers = await _caretakerService.GetAvailableCaretakersAsync(serviceType, area);
            return Ok(ApiResponse<List<CaretakerResponse>>.Ok("Caretakers retrieved", caretakers));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretakers");
            return StatusCode(500, ApiResponse<List<CaretakerResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Get caretaker profile with ratings
    /// </summary>
    [HttpGet("{id}")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CaretakerResponse>>> GetCaretaker(string id)
    {
        try
        {
            var caretaker = await _caretakerService.GetCaretakerProfileAsync(id);
            if (caretaker == null)
                return NotFound(ApiResponse<CaretakerResponse>.NotFound("Caretaker"));

            return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker retrieved", caretaker));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving caretaker");
            return StatusCode(500, ApiResponse<CaretakerResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Assign caretaker to property (Landlord only)
    /// </summary>
    [HttpPost("assign")]
    [Authorize(Roles = "Landlord")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CaretakerResponse>>> AssignCaretaker([FromBody] AssignCaretakerRequest request)
    {
        try
        {
            var landlordId = User.GetUserId();
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<CaretakerResponse>.UnAuthorized());

            await _caretakerService.AssignCaretakerToPropertyAsync(request.PropertyId, request.CaretakerId, landlordId);
            return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker assigned", null));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CaretakerResponse>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning caretaker");
            return StatusCode(500, ApiResponse<CaretakerResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Create service request
    /// </summary>
    [HttpPost("service-requests")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> CreateServiceRequest([FromBody] CreateServiceRequestRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

            var serviceRequest = await _caretakerService.CreateServiceRequestAsync(request, userId);
            return StatusCode(201, ApiResponse<ServiceRequestResponse>.Created("Service Request", serviceRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating service request");
            return StatusCode(500, ApiResponse<ServiceRequestResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Get service requests (role-aware)
    /// </summary>
    [HttpGet("service-requests/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<ServiceRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ServiceRequestResponse>>>> GetMyServiceRequests()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<List<ServiceRequestResponse>>.UnAuthorized());

            var requests = await _caretakerService.GetServiceRequestsAsync(userId);
            return Ok(ApiResponse<List<ServiceRequestResponse>>.Ok("Service requests retrieved", requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving service requests");
            return StatusCode(500, ApiResponse<List<ServiceRequestResponse>>.InternalServerError());
        }
    }

    /// <summary>
    /// Accept service request
    /// </summary>
    [HttpPatch("service-requests/{id}/accept")]
    [Authorize(Roles = "Caretaker")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> AcceptServiceRequest(string id)
    {
        try
        {
            var caretakerId = User.GetUserId();
            if (string.IsNullOrEmpty(caretakerId))
                return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

            await _caretakerService.AcceptServiceRequestAsync(id, caretakerId);
            return Ok(ApiResponse<ServiceRequestResponse>.Ok("Service request accepted", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting service request");
            return StatusCode(500, ApiResponse<ServiceRequestResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Update service request status
    /// </summary>
    [HttpPatch("service-requests/{id}/status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> UpdateServiceRequestStatus(string id, [FromBody] UpdateServiceRequestStatusRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

            await _caretakerService.UpdateServiceRequestStatusAsync(id, request.Status, userId);
            return Ok(ApiResponse<ServiceRequestResponse>.Ok("Status updated", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status");
            return StatusCode(500, ApiResponse<ServiceRequestResponse>.InternalServerError());
        }
    }

    /// <summary>
    /// Submit review for completed service
    /// </summary>
    [HttpPost("service-requests/{id}/review")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> ReviewService(string id, [FromBody] SubmitServiceReviewRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

            await _caretakerService.SubmitServiceReviewAsync(id, userId, request.Rating, request.Comment);
            return StatusCode(201, ApiResponse<ServiceRequestResponse>.Ok("Review submitted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting review");
            return StatusCode(500, ApiResponse<ServiceRequestResponse>.InternalServerError());
        }
    }
}
