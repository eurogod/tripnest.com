using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Caretakers;
using TripNest.Core.DTOs.Maintenance;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;

    public MaintenanceController(IMaintenanceService maintenanceService)
    {
        _maintenanceService = maintenanceService;
    }

    /// <summary>
    /// Report a maintenance issue
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<MaintenanceResponse>>> ReportMaintenance([FromBody] CreateMaintenanceRequest request)
    {
        var tenantId = User.GetUserId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized(ApiResponse<MaintenanceResponse>.UnAuthorized());

        var maintenance = await _maintenanceService.ReportMaintenanceAsync(request, tenantId);
        return Created($"api/maintenance-requests/{maintenance.MaintenanceId}", ApiResponse<MaintenanceResponse>.Created("Maintenance Request", maintenance));
    }

    /// <summary>
    /// Get maintenance requests for a property (Landlord view)
    /// </summary>
    [HttpGet("property/{propertyId}")]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MaintenanceResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenanceResponse>>>> GetPropertyMaintenance(string propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PagedResult<MaintenanceResponse>>.UnAuthorized());

        var requests = await _maintenanceService.GetPropertyMaintenanceAsync(propertyId, landlordId, page, pageSize);
        return Ok(ApiResponse<PagedResult<MaintenanceResponse>>.Ok("Maintenance requests retrieved", requests));
    }

    /// <summary>
    /// Get current user's maintenance requests (Tenant view)
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Tenant")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MaintenanceResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<MaintenanceResponse>>>> GetMyMaintenance([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var tenantId = User.GetUserId();
        if (string.IsNullOrEmpty(tenantId))
            return Unauthorized(ApiResponse<PagedResult<MaintenanceResponse>>.UnAuthorized());

        var requests = await _maintenanceService.GetTenantMaintenanceAsync(tenantId, page, pageSize);
        return Ok(ApiResponse<PagedResult<MaintenanceResponse>>.Ok("Maintenance requests retrieved", requests));
    }

    /// <summary>
    /// Update maintenance request status
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<MaintenanceResponse>>> UpdateMaintenanceStatus(string id, [FromBody] UpdateMaintenanceStatusRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<MaintenanceResponse>.UnAuthorized());

        await _maintenanceService.UpdateMaintenanceStatusAsync(id, request.Status, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<MaintenanceResponse>.Ok("Status updated", null));
    }

    /// <summary>
    /// Convert maintenance request to service request
    /// </summary>
    [HttpPost("{id}/convert-to-service-request")]
    [Authorize(Roles = "Landlord,Admin")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<ServiceRequestResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ServiceRequestResponse>>> ConvertToServiceRequest(string id, [FromBody] ConvertToServiceRequestRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<ServiceRequestResponse>.UnAuthorized());

        var serviceRequest = await _maintenanceService.ConvertToServiceRequestAsync(id, request.CaretakerId, landlordId);
        return Created($"api/service-requests/{serviceRequest.ServiceRequestId}", ApiResponse<ServiceRequestResponse>.Created("Service Request", serviceRequest));
    }
}
