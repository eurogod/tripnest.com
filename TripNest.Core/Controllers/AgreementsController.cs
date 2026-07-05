using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Agreements;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;
using TripNest.Core.Extensions;

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
    [ProducesResponseType(typeof(ApiResponse<List<AgreementResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<AgreementResponse>>>> GetMyAgreements()
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(ApiResponse<List<AgreementResponse>>.UnAuthorized());

            var agreements = await _agreementService.GetUserAgreementsAsync(userId);
            return Ok(ApiResponse<List<AgreementResponse>>.Ok("Agreements retrieved", agreements));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving agreements");
            return StatusCode(500, ApiResponse<List<AgreementResponse>>.InternalServerError());
        }
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

    /// <summary>
    /// Download agreement PDF
    /// </summary>
    [HttpGet("{id}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAgreement(string id)
    {
        try
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var (pdf, filename) = await _agreementService.DownloadAgreementPdfAsync(id, userId);
            if (pdf == null)
                return NotFound();

            return File(pdf, "application/pdf", filename);
        }
        catch (InvalidOperationException)
        {
            // Service throws this when the agreement doesn't exist / isn't the user's — that's a 404, not a 500.
            return NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading agreement");
            return StatusCode(500);
        }
    }
}
