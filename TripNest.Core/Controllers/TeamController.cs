using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/team")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class TeamController : ControllerBase
{
    private readonly ITeamService _teamService;

    public TeamController(ITeamService teamService) => _teamService = teamService;

    /// <summary>List the caller's team members.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TeamMemberResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<TeamMemberResponse>>>> GetMine()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<List<TeamMemberResponse>>.UnAuthorized());

        var members = await _teamService.GetMineAsync(landlordId);
        return Ok(ApiResponse<List<TeamMemberResponse>>.Ok("Team retrieved", members));
    }

    /// <summary>Invite a new team member.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TeamMemberResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<TeamMemberResponse>>> Invite([FromBody] InviteTeamMemberRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<TeamMemberResponse>.UnAuthorized());

        var member = await _teamService.InviteAsync(request, landlordId);
        return Created($"api/team/{member.Id}", ApiResponse<TeamMemberResponse>.Created("Team member", member));
    }

    /// <summary>Update a team member's role or status.</summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TeamMemberResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<TeamMemberResponse>>> Update(string id, [FromBody] UpdateTeamMemberRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<TeamMemberResponse>.UnAuthorized());

        var member = await _teamService.UpdateAsync(id, request, landlordId);
        return Ok(ApiResponse<TeamMemberResponse>.Ok("Team member updated", member));
    }

    /// <summary>Remove a team member.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Remove(string id)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _teamService.RemoveAsync(id, landlordId);
        return Ok(ApiResponse<object>.Ok("Team member removed"));
    }
}
