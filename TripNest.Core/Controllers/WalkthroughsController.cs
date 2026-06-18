using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Properties;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/properties/{propertyId}/walkthroughs")]
[Produces("application/json")]
public class WalkthroughsController : ControllerBase
{
    private readonly IWalkthroughService _walkthroughService;
    private readonly ILogger<WalkthroughsController> _logger;

    public WalkthroughsController(IWalkthroughService walkthroughService, ILogger<WalkthroughsController> logger)
    {
        _walkthroughService = walkthroughService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalkthroughResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<WalkthroughResponse>>> CreateWalkthrough(string propertyId, [FromBody] CreateWalkthroughRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.BadRequest("Invalid request data"));

            request.PropertyId = propertyId;
            var response = await _walkthroughService.CreateWalkthroughAsync(request);

            return Created($"api/properties/{propertyId}/walkthroughs/{response.WalkthroughId}", ApiResponse<WalkthroughResponse>.Created("Walkthrough", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating walkthrough");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<WalkthroughResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<WalkthroughResponse>>>> GetPropertyWalkthroughs(string propertyId)
    {
        try
        {
            var response = await _walkthroughService.GetPropertyWalkthroughsAsync(propertyId);
            return Ok(ApiResponse<IEnumerable<WalkthroughResponse>>.Ok("Walkthroughs retrieved", response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving walkthroughs");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpGet("{walkthroughId}")]
    [ProducesResponseType(typeof(ApiResponse<WalkthroughResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<WalkthroughResponse>>> GetWalkthrough(string propertyId, string walkthroughId)
    {
        try
        {
            var response = await _walkthroughService.GetWalkthroughAsync(walkthroughId);
            return Ok(ApiResponse<WalkthroughResponse>.Ok("Walkthrough retrieved", response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving walkthrough");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpDelete("{walkthroughId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteWalkthrough(string propertyId, string walkthroughId)
    {
        try
        {
            await _walkthroughService.DeleteWalkthroughAsync(walkthroughId);
            return Ok(ApiResponse<object>.Ok("Walkthrough deleted successfully", new { }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting walkthrough");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
