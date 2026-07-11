using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/statements")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class StatementsController : ControllerBase
{
    private readonly IStatementService _statementService;

    public StatementsController(IStatementService statementService) => _statementService = statementService;

    /// <summary>Monthly payout statements for the caller (gross, management fee, net).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StatementResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<StatementResponse>>>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<PagedResult<StatementResponse>>.UnAuthorized());

        var statements = await _statementService.GetForLandlordAsync(landlordId, page, pageSize);
        return Ok(ApiResponse<PagedResult<StatementResponse>>.Ok("Statements retrieved", statements));
    }
}
