using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Agreements;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.DTOs.Shared;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AgreementsController : ControllerBase
{
    private readonly IAgreementService _agreementService;
    private readonly ILogger<AgreementsController> _logger;

    public AgreementsController(IAgreementService agreementService, ILogger<AgreementsController> logger)
    {
        _agreementService = agreementService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new rental agreement for a booking
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AgreementResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<AgreementResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AgreementResponse>>> CreateAgreement([FromBody] CreateAgreementRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<AgreementResponse>.UnAuthorized());

        var agreement = await _agreementService.CreateAgreementAsync(request.BookingId, userId);
        return Created($"api/agreements/{agreement.AgreementId}", ApiResponse<AgreementResponse>.Created("Agreement", agreement));
    }

    /// <summary>
    /// Get all agreements for current user
    /// </summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AgreementResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AgreementResponse>>>> GetMyAgreements([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<PagedResult<AgreementResponse>>.UnAuthorized());

        var agreements = await _agreementService.GetUserAgreementsAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<AgreementResponse>>.Ok("Agreements retrieved", agreements));
    }

    /// <summary>
    /// Get agreement details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<AgreementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AgreementResponse>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<AgreementResponse>>> GetAgreement(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<AgreementResponse>.UnAuthorized());

        var agreement = await _agreementService.GetAgreementAsync(id, userId);
        if (agreement == null)
            return NotFound(ApiResponse<AgreementResponse>.NotFound("Agreement"));

        return Ok(ApiResponse<AgreementResponse>.Ok("Agreement retrieved", agreement));
    }

    /// <summary>
    /// Sign an agreement
    /// </summary>
    [HttpPost("{id}/sign")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> SignAgreement(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _agreementService.SignAgreementAsync(id, userId);
        return Ok(ApiResponse<object>.Ok("Agreement signed", null));
    }

    /// <summary>Plain-language AI explanation of the agreement in the caller's preferred language
    /// (parties only). Advisory — the signed terms remain the binding text.</summary>
    [HttpGet("{id}/summary")]
    [ProducesResponseType(typeof(ApiResponse<TripNest.Core.DTOs.Ai.AgreementSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TripNest.Core.DTOs.Ai.AgreementSummaryResponse>>> GetSummary(
        string id, [FromServices] IAiInsightsService aiInsights)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var summary = await aiInsights.GetAgreementSummaryAsync(id, userId);
        return Ok(ApiResponse<TripNest.Core.DTOs.Ai.AgreementSummaryResponse>.Ok("Agreement summary", summary));
    }

    public record TerminateAgreementRequest(string Reason);

    /// <summary>Terminates a signed agreement (either party; record-keeping — money flows are
    /// handled by booking cancellation / escrow, not here).</summary>
    [HttpPost("{id}/terminate")]
    [ProducesResponseType(typeof(ApiResponse<AgreementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AgreementResponse>>> TerminateAgreement(string id, [FromBody] TerminateAgreementRequest request)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        var agreement = await _agreementService.TerminateAgreementAsync(id, userId, request.Reason);
        return Ok(ApiResponse<AgreementResponse>.Ok("Agreement terminated", agreement));
    }

    /// <summary>
    /// Download agreement PDF
    /// </summary>
    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAgreement(string id)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // NotFoundException → 404 and ForbiddenException → 403 via the middleware (a non-party
        // caller now gets 403 instead of the old catch-all's 500).
        var (pdf, filename) = await _agreementService.DownloadAgreementPdfAsync(id, userId);
        if (pdf == null)
            return NotFound();

        return File(pdf, "application/pdf", filename);
    }
}
