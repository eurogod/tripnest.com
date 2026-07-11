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

    /// <summary>
    /// Property search with the full filter set: text location, bedrooms, stay type, property
    /// type, amenities (CSV, all required), nightly-price range, a map viewport (the client's
    /// visible bounds as it pans/zooms), and stay dates. Dates restrict results to available
    /// listings and attach a per-result <c>quote</c> — the exact all-in total for that stay.
    /// </summary>
    [HttpGet("search")]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PropertyResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<PropertyResponse>>>> SearchProperties(
        [FromQuery] string? location = null,
        [FromQuery] int minBedrooms = 1,
        [FromQuery] int maxBedrooms = 10,
        [FromQuery] Enums.StayType? stayType = null,
        [FromQuery] string? propertyType = null,
        [FromQuery] string? amenities = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] double? minLat = null,
        [FromQuery] double? maxLat = null,
        [FromQuery] double? minLng = null,
        [FromQuery] double? maxLng = null,
        [FromQuery] DateTime? checkIn = null,
        [FromQuery] DateTime? checkOut = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        var criteria = new DTOs.Search.PropertySearchCriteria
        {
            Location = location,
            MinBedrooms = minBedrooms,
            MaxBedrooms = maxBedrooms,
            StayType = stayType,
            PropertyType = propertyType,
            Amenities = amenities,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            MinLat = minLat,
            MaxLat = maxLat,
            MinLng = minLng,
            MaxLng = maxLng,
            CheckIn = checkIn,
            CheckOut = checkOut
        };

        // The query pages in the database, but the body keeps the original array shape existing
        // clients iterate — pagination metadata travels in headers instead of a wrapper object.
        // Default pageSize is the clamp maximum so legacy callers see as much as one page allows.
        var result = await _propertyService.SearchPropertiesAsync(criteria, page, pageSize);
        Response.Headers["X-Total-Count"] = result.TotalCount.ToString();
        Response.Headers["X-Page"] = result.Page.ToString();
        Response.Headers["X-Page-Size"] = result.PageSize.ToString();
        Response.Headers["X-Total-Pages"] = result.TotalPages.ToString();
        return Ok(ApiResponse<IEnumerable<PropertyResponse>>.Ok("Properties found", result.Items));
    }

    /// <summary>
    /// True-total price breakdown for a stay: nightly subtotal (weekend rates included), cleaning
    /// fee, length-of-stay discount, and — when the caller is signed in — their loyalty discount.
    /// This is the exact amount booking will charge; no fee is added later.
    /// </summary>
    [HttpGet("{propertyId}/quote")]
    [ProducesResponseType(typeof(ApiResponse<StayQuote>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<StayQuote>>> GetStayQuote(
        string propertyId, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut)
    {
        var quote = await _propertyService.GetStayQuoteAsync(propertyId, checkIn, checkOut, User.GetUserId());
        return Ok(ApiResponse<StayQuote>.Ok("Stay quote calculated", quote));
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

    /// <summary>
    /// Drafts AI listing copy (title, description, highlights) from the property's facts and
    /// photos, for the owner to review and apply — never auto-applied. Returns 400 with a clear
    /// message when AI is not configured on this server.
    /// </summary>
    [HttpPost("{propertyId}/generate-copy")]
    [Authorize]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<TripNest.Core.DTOs.Properties.ListingCopySuggestion>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TripNest.Core.DTOs.Properties.ListingCopySuggestion>>> GenerateListingCopy(string propertyId)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var suggestion = await _propertyService.GenerateListingCopyAsync(propertyId, userId);
        return Ok(ApiResponse<TripNest.Core.DTOs.Properties.ListingCopySuggestion>.Ok("Listing copy generated", suggestion));
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
