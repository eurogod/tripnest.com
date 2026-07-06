using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
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
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

        var response = await _propertyService.CreatePropertyAsync(userId, request);

        return Created($"api/properties/{response.PropertyId}", ApiResponse<PropertyResponse>.Created("Property", response));
    }

    [HttpPut("{propertyId}")]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> UpdateProperty(string propertyId, [FromBody] CreatePropertyRequest request)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

        var response = await _propertyService.UpdatePropertyAsync(propertyId, userId, User.IsInRole("Admin"), request);

        return Ok(ApiResponse<PropertyResponse>.Ok("Property updated successfully", response));
    }

    [HttpGet("{propertyId}")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<PropertyResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PropertyResponse>>> GetProperty(string propertyId)
    {
        var response = await _propertyService.GetPropertyAsync(propertyId);
        return Ok(ApiResponse<PropertyResponse>.Ok("Property retrieved successfully", response));
    }

    [HttpGet("user/my-properties")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetUserProperties()
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var response = await _propertyService.GetUserPropertiesAsync(userId);
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("User properties retrieved", response));
    }

    [HttpGet]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetAllActiveProperties()
    {
        var response = await _propertyService.GetAllActivePropertiesAsync();
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Active properties retrieved", response));
    }

    /// <summary>Featured listings for the home page (most recent active properties).</summary>
    [HttpGet("featured")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> GetFeaturedProperties([FromQuery] int limit = 8)
    {
        var active = await _propertyService.GetAllActivePropertiesAsync();
        var featured = active.Take(limit < 1 ? 8 : limit);
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Featured properties retrieved", featured));
    }

    [HttpGet("search")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> SearchProperties(
        [FromQuery] string location,
        [FromQuery] int minBedrooms = 1,
        [FromQuery] int maxBedrooms = 10,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        // The query pages in the database, but the body keeps the original array shape existing
        // clients iterate — pagination metadata travels in headers instead of a wrapper object.
        // Default pageSize is the clamp maximum so legacy callers see as much as one page allows.
        var result = await _propertyService.SearchPropertiesAsync(location, minBedrooms, maxBedrooms, page, pageSize);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Page"] = result.Page.ToString();
        Response.Headers["X-Page-Size"] = result.PageSize.ToString();
        Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Properties found", result.Items));
    }

    [HttpDelete("{propertyId}")]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProperty(string propertyId)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var hardDeleted = await _propertyService.DeletePropertyAsync(propertyId, userId, User.IsInRole("Admin"));
        return Ok(ApiResponse<object>.Ok(
            hardDeleted
                ? "Property deleted successfully"
                : "Property archived — it has booking history, so records are retained but it is no longer listed",
            new { }));
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
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var paths = await _propertyService.AddPhotosAsync(propertyId, userId, Request.Form.Files);
        return Ok(ApiResponse<List<string>>.Ok("Photos uploaded", paths));
    }
}
