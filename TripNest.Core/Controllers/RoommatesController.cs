using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Roommates;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

/// <summary>
/// Roommate matching for students and long-term renters: an opt-in profile (budget, location,
/// university, living habits) and a compatibility-ranked match list. From a match, the client
/// starts a chat (POST api/chat conversations) and later a group booking with split billing.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class RoommatesController : ControllerBase
{
    private readonly IRoommateService _roommateService;

    public RoommatesController(IRoommateService roommateService) => _roommateService = roommateService;

    /// <summary>The caller's roommate profile (404 until created).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<RoommateProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<RoommateProfileResponse>>> GetMyProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var profile = await _roommateService.GetMyProfileAsync(userId);
        return Ok(ApiResponse<RoommateProfileResponse>.Ok("Roommate profile retrieved", profile));
    }

    /// <summary>Creates or updates the caller's roommate profile.</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(ApiResponse<RoommateProfileResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RoommateProfileResponse>>> UpsertMyProfile(
        [FromBody] UpsertRoommateProfileRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var profile = await _roommateService.UpsertMyProfileAsync(userId, request);
        return Ok(ApiResponse<RoommateProfileResponse>.Ok("Roommate profile saved", profile));
    }

    /// <summary>Removes the caller's roommate profile (and with it, access to matching).</summary>
    [HttpDelete("me")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMyProfile()
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _roommateService.DeleteMyProfileAsync(userId);
        return Ok(ApiResponse<object>.Ok("Roommate profile removed", new { }));
    }

    /// <summary>
    /// Compatibility-ranked roommate matches (best first). Requires the caller's own visible
    /// profile; smoking/pets hard conflicts never appear. Filters are optional narrowing.
    /// </summary>
    [HttpGet("matches")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RoommateMatchResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PagedResult<RoommateMatchResponse>>>> GetMatches(
        [FromQuery] string? location = null,
        [FromQuery] decimal? maxBudget = null,
        [FromQuery] string? university = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var matches = await _roommateService.GetMatchesAsync(userId, location, maxBudget, university, page, pageSize);
        return Ok(ApiResponse<PagedResult<RoommateMatchResponse>>.Ok("Roommate matches retrieved", matches));
    }

    /// <summary>AI explanation of why the caller and a matched profile fit — sentences on top of
    /// the numeric score, plus things worth discussing before moving in (cached per pair).</summary>
    [HttpGet("matches/{otherUserId}/explanation")]
    [ProducesResponseType(typeof(ApiResponse<TripNest.Core.DTOs.Ai.RoommateExplanationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TripNest.Core.DTOs.Ai.RoommateExplanationResponse>>> ExplainMatch(
        string otherUserId, [FromServices] IAiInsightsService aiInsights)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var explanation = await aiInsights.ExplainRoommateMatchAsync(userId, otherUserId);
        return Ok(ApiResponse<TripNest.Core.DTOs.Ai.RoommateExplanationResponse>.Ok("Match explanation", explanation));
    }
}
