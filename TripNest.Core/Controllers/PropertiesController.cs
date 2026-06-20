using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _propertyService;
    private readonly ILogger<PropertiesController> _logger;

    public PropertiesController(IPropertyService propertyService, ILogger<PropertiesController> logger)
    {
        _propertyService = propertyService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> CreateProperty([FromBody] CreatePropertyRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var response = await _propertyService.CreatePropertyAsync(userId, request);

            return Created($"api/properties/{response.PropertyId}", ApiResponse<PropertyResponse>.Created("Property", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating property");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPut("{propertyId}")]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> UpdateProperty(string propertyId, [FromBody] CreatePropertyRequest request)
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            var response = await _propertyService.UpdatePropertyAsync(propertyId, request);

            return Ok(ApiResponse<PropertyResponse>.Ok("Property updated successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating property");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> GetProperty(string propertyId)
    {
        try
        {
            var response = await _propertyService.GetPropertyAsync(propertyId);
            return Ok(ApiResponse<PropertyResponse>.Ok("Property retrieved successfully", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving property");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("user/my-properties")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetUserProperties()
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var response = await _propertyService.GetUserPropertiesAsync(userId);
            return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("User properties retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user properties");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetAllActiveProperties()
    {
        try
        {
            var response = await _propertyService.GetAllActivePropertiesAsync();
            return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Active properties retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active properties");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> SearchProperties([FromQuery] string location, [FromQuery] int minBedrooms = 1, [FromQuery] int maxBedrooms = 10)
    {
        try
        {
            var response = await _propertyService.SearchPropertiesAsync(location, minBedrooms, maxBedrooms);
            return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Properties found", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching properties");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpDelete("{propertyId}")]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProperty(string propertyId)
    {
        try
        {
            var userId = User.GetUserId();

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            await _propertyService.DeletePropertyAsync(propertyId);
            return Ok(ApiResponse<object>.Ok("Property deleted successfully", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting property");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    /// <summary>Uploads one or more photos for a property (owner only, multipart/form-data).</summary>
    [HttpPost("{propertyId}/photos")]
    [Authorize]
    [RequireVerified]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<string>>>> UploadPhotos(string propertyId)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var paths = await _propertyService.AddPhotosAsync(propertyId, userId, Request.Form.Files);
            return Ok(ApiResponse<List<string>>.Ok("Photos uploaded", paths));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading property photos");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
