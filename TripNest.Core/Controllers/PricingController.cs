using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/pricing")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingController(IPricingService pricingService) => _pricingService = pricingService;

    /// <summary>Get the pricing rules for one of the caller's listings (defaults derived from the listing if unset).</summary>
    [HttpGet("{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<PricingSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PricingSettingsResponse>>> Get(string propertyId)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PricingSettingsResponse>.UnAuthorized());

        var settings = await _pricingService.GetAsync(propertyId, landlordId);
        return Ok(ApiResponse<PricingSettingsResponse>.Ok("Pricing retrieved", settings));
    }

    /// <summary>Create or update the pricing rules for one of the caller's listings.</summary>
    [HttpPut("{propertyId}")]
    [ProducesResponseType(typeof(ApiResponse<PricingSettingsResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PricingSettingsResponse>>> Update(string propertyId, [FromBody] UpdatePricingSettingsRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PricingSettingsResponse>.UnAuthorized());

        var settings = await _pricingService.UpdateAsync(propertyId, request, landlordId);
        return Ok(ApiResponse<PricingSettingsResponse>.Ok("Pricing updated", settings));
    }
}
