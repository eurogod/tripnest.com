using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/properties/{propertyId}/tour")]
[Produces("application/json")]
public class PropertyTourController : ControllerBase
{
    private readonly ITourService _tourService;

    public PropertyTourController(ITourService tourService) => _tourService = tourService;

    /// <summary>Get a listing's virtual tour (rooms + hotspots). Public.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PropertyTourResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PropertyTourResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<PropertyTourResponse>>> Get(string propertyId)
    {
        var tour = await _tourService.GetAsync(propertyId);
        return tour is null
            ? NotFound(ApiResponse<PropertyTourResponse>.NotFound("Tour"))
            : Ok(ApiResponse<PropertyTourResponse>.Ok("Tour retrieved", tour));
    }

    /// <summary>Create or replace a listing's virtual tour (owner only).</summary>
    [HttpPut]
    [Authorize(Roles = "Landlord,Admin")]
    [ProducesResponseType(typeof(ApiResponse<PropertyTourResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PropertyTourResponse>>> Upsert(string propertyId, [FromBody] UpsertPropertyTourRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PropertyTourResponse>.UnAuthorized());

        var tour = await _tourService.UpsertAsync(propertyId, request, landlordId);
        return Ok(ApiResponse<PropertyTourResponse>.Ok("Tour saved", tour));
    }
}
