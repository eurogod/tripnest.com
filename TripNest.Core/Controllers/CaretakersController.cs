using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using System.Security.Claims;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.DTOs.Shared;
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
    /// Get available caretakers, optionally filtered by service type and area (paged)
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CaretakerResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CaretakerResponse>>>> GetCaretakers(
        [FromQuery] string? serviceType, [FromQuery] string? area,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var caretakers = await _caretakerService.GetAvailableCaretakersAsync(serviceType, area, page, pageSize);
        return Ok(ApiResponse<PagedResult<CaretakerResponse>>.Ok("Caretakers retrieved", caretakers));
    }

    /// <summary>
    /// The caller's own directory profile (404 until they create one via PUT /api/caretakers/me).
    /// </summary>
    [HttpGet("me")]
    [Authorize(Roles = "Caretaker,Admin")]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CaretakerResponse>>> GetMyProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<CaretakerResponse>.UnAuthorized());

        var profile = await _caretakerService.GetMyProfileAsync(userId);
        if (profile is null)
            return NotFound(ApiResponse<CaretakerResponse>.NotFound("Caretaker profile"));

        return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker profile retrieved", profile));
    }

    /// <summary>
    /// Create or update the caller's public directory profile — without it a Caretaker-role account
    /// never appears in the caretakers list and cannot be assigned or hired. Requires identity
    /// verification, like other caretaker actions.
    /// </summary>
    [HttpPut("me")]
    [Authorize(Roles = "Caretaker,Admin")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<CaretakerResponse>>> UpsertMyProfile([FromBody] UpsertCaretakerProfileRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<CaretakerResponse>.UnAuthorized());

        var profile = await _caretakerService.UpsertMyProfileAsync(userId, request);
        return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker profile saved", profile));
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
        var caretaker = await _caretakerService.GetCaretakerProfileAsync(id);
        if (caretaker == null)
            return NotFound(ApiResponse<CaretakerResponse>.NotFound("Caretaker"));

        return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker retrieved", caretaker));
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
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<CaretakerResponse>.UnAuthorized());

        await _caretakerService.AssignCaretakerToPropertyAsync(request.PropertyId, request.CaretakerId, landlordId);
        return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker assigned", null));
    }

    /// <summary>
    /// End the active caretaker assignment on one of the caller's properties (Landlord only)
    /// </summary>
    [HttpPost("unassign")]
    [Authorize(Roles = "Landlord")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<CaretakerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CaretakerResponse>>> UnassignCaretaker([FromBody] AssignCaretakerRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<CaretakerResponse>.UnAuthorized());

        await _caretakerService.UnassignCaretakerFromPropertyAsync(request.PropertyId, request.CaretakerId, landlordId);
        return Ok(ApiResponse<CaretakerResponse>.Ok("Caretaker unassigned", null));
    }

    /// <summary>
    /// Assignments the caller is party to — on their properties and/or as the caretaker.
    /// </summary>
    [HttpGet("assignments/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CaretakerAssignmentResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<CaretakerAssignmentResponse>>>> GetMyAssignments([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<CaretakerAssignmentResponse>>.UnAuthorized());

        var assignments = await _caretakerService.GetMyAssignmentsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<CaretakerAssignmentResponse>>.Ok("Assignments retrieved", assignments));
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
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        var serviceRequest = await _caretakerService.CreateServiceRequestAsync(request, userId);
        return StatusCode(201, ApiResponse<ServiceRequestResponse>.Created("Service Request", serviceRequest));
    }

    /// <summary>
    /// Get service requests (role-aware)
    /// </summary>
    [HttpGet("service-requests/mine")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ServiceRequestResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<ServiceRequestResponse>>>> GetMyServiceRequests([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<ServiceRequestResponse>>.UnAuthorized());

        var requests = await _caretakerService.GetServiceRequestsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<ServiceRequestResponse>>.Ok("Service requests retrieved", requests));
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
        var caretakerId = User.GetUserId();
        if (string.IsNullOrEmpty(caretakerId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        await _caretakerService.AcceptServiceRequestAsync(id, caretakerId);
        return Ok(ApiResponse<ServiceRequestResponse>.Ok("Service request accepted", null));
    }

    /// <summary>
    /// Decline a pending service request (assigned caretaker only)
    /// </summary>
    [HttpPatch("service-requests/{id}/decline")]
    [Authorize(Roles = "Caretaker")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> DeclineServiceRequest(string id)
    {
        var caretakerId = User.GetUserId();
        if (string.IsNullOrEmpty(caretakerId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        await _caretakerService.DeclineServiceRequestAsync(id, caretakerId);
        return Ok(ApiResponse<ServiceRequestResponse>.Ok("Service request declined", null));
    }

    /// <summary>
    /// Update service request status (transitions limited by role — see service)
    /// </summary>
    [HttpPatch("service-requests/{id}/status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> UpdateServiceRequestStatus(string id, [FromBody] UpdateServiceRequestStatusRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        await _caretakerService.UpdateServiceRequestStatusAsync(id, request.Status, userId);
        return Ok(ApiResponse<ServiceRequestResponse>.Ok("Status updated", null));
    }

    /// <summary>
    /// Submit review for completed service
    /// </summary>
    [HttpPost("service-requests/{id}/review")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> ReviewService(string id, [FromBody] SubmitServiceReviewRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        await _caretakerService.SubmitServiceReviewAsync(id, userId, request.Rating, request.Comment);
        return StatusCode(201, ApiResponse<ServiceRequestResponse>.Ok("Review submitted successfully"));
    }
}
