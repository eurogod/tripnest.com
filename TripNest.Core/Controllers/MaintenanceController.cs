using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Maintenance;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IMaintenanceService maintenanceService, ILogger<MaintenanceController> logger)
    {
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    /// <summary>
    /// Report a maintenance issue
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ReportMaintenance([FromBody] CreateMaintenanceRequest request)
    {
        try
        {
            var tenantId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var maintenance = await _maintenanceService.ReportMaintenanceAsync(request, tenantId);
            var mainId = ((dynamic)maintenance).Id;
            return Created($"api/maintenance-requests/{mainId}", ApiResponse<object>.Created("Maintenance Request", maintenance));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting maintenance");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get maintenance requests for a property (Landlord view)
    /// </summary>
    [HttpGet("property/{propertyId}")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetPropertyMaintenance(string propertyId)
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var requests = await _maintenanceService.GetPropertyMaintenanceAsync(propertyId, landlordId);
            return Ok(ApiResponse<object>.Ok("Maintenance requests retrieved", requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance requests");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Get current user's maintenance requests (Tenant view)
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> GetMyMaintenance()
    {
        try
        {
            var tenantId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(tenantId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var requests = await _maintenanceService.GetTenantMaintenanceAsync(tenantId);
            return Ok(ApiResponse<object>.Ok("Maintenance requests retrieved", requests));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance requests");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Update maintenance request status
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateMaintenanceStatus(string id, [FromBody] UpdateMaintenanceStatusRequest request)
    {
        try
        {
            var userId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _maintenanceService.UpdateMaintenanceStatusAsync(id, request.Status, userId);
            return Ok(ApiResponse<object>.Ok("Status updated", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>
    /// Convert maintenance request to service request
    /// </summary>
    [HttpPost("{id}/convert-to-service-request")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ConvertToServiceRequest(string id, [FromBody] ConvertToServiceRequestRequest request)
    {
        try
        {
            var landlordId = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var serviceRequest = await _maintenanceService.ConvertToServiceRequestAsync(id, request.CaretakerId, landlordId);
            var srId = ((dynamic)serviceRequest).Id;
            return Created($"api/service-requests/{srId}", ApiResponse<object>.Created("Service Request", serviceRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting to service request");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
