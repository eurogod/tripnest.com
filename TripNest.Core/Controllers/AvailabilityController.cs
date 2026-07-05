using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Response;
using TripNest.Core.Extensions;
using TripNest.Core.Filters;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/properties/{propertyId}")]
[Produces("application/json")]
public class AvailabilityController : ControllerBase
{
    private readonly IRepository<PropertyBlockedDate> _blockedDateRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IAvailabilityService _availabilityService;
    private readonly ILogger<AvailabilityController> _logger;

    public AvailabilityController(
        IRepository<PropertyBlockedDate> blockedDateRepository,
        IPropertyRepository propertyRepository,
        IAvailabilityService availabilityService,
        ILogger<AvailabilityController> logger)
    {
        _blockedDateRepository = blockedDateRepository;
        _propertyRepository = propertyRepository;
        _availabilityService = availabilityService;
        _logger = logger;
    }

    /// <summary>Open (bookable) date ranges for the calendar widget within [from, to].</summary>
    [HttpGet("available-ranges")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DateRange>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<DateRange>>>> GetAvailableRanges(
        string propertyId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from ?? DateTime.UtcNow.Date;
        var end = to ?? start.AddMonths(3);
        var ranges = await _availabilityService.GetAvailableRanges(propertyId, start, end);
        return Ok(ApiResponse<IEnumerable<DateRange>>.Ok("Available ranges retrieved", ranges));
    }

    [HttpGet("availability")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<object>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetAvailability(
        string propertyId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property is null)
                return NotFound(ApiResponse<object>.NotFound("Property"));

            // Filter by property and (optional) date window in the database rather than loading
            // every property's blocked dates into memory.
            var start = startDate;
            var end = endDate;
            var blockedDates = await _blockedDateRepository.FindAsync(b =>
                b.PropertyId == propertyId &&
                (!start.HasValue || b.EndDate >= start.Value) &&
                (!end.HasValue || b.StartDate <= end.Value));

            var filtered = blockedDates
                .Select(b => (object)new
                {
                    b.Id,
                    b.StartDate,
                    b.EndDate,
                    b.Reason
                })
                .ToList();

            return Ok(ApiResponse<IEnumerable<object>>.Ok("Blocked dates retrieved", filtered));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving availability for property {PropertyId}", propertyId);
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpPost("blocked-dates")]
    [Authorize(Roles = "Landlord")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> CreateBlockedDate(
        string propertyId,
        [FromBody] BlockDateRequest request)
    {
        try
        {
            var landlordId = User.GetUserId();
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property is null)
                return NotFound(ApiResponse<object>.NotFound("Property"));

            if (property.UserId != landlordId)
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            if (request.StartDate >= request.EndDate)
                return BadRequest(ApiResponse<object>.BadRequest("StartDate must be before EndDate"));

            var blockedDate = new PropertyBlockedDate
            {
                PropertyId = propertyId,
                BlockedByUserId = landlordId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                Reason = request.Reason
            };

            var created = await _blockedDateRepository.AddAsync(blockedDate);
            await _blockedDateRepository.SaveChangesAsync();

            var result = (object)new
            {
                created.Id,
                created.StartDate,
                created.EndDate,
                created.Reason
            };

            return Created($"api/properties/{propertyId}/blocked-dates/{created.Id}",
                ApiResponse<object>.Created("BlockedDate", result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating blocked date for property {PropertyId}", propertyId);
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }

    [HttpDelete("blocked-dates/{blockedDateId}")]
    [Authorize(Roles = "Landlord")]
    [RequireVerified]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteBlockedDate(
        string propertyId,
        string blockedDateId)
    {
        try
        {
            var landlordId = User.GetUserId();
            if (string.IsNullOrEmpty(landlordId))
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var property = await _propertyRepository.GetByIdAsync(propertyId);
            if (property is null)
                return NotFound(ApiResponse<object>.NotFound("Property"));

            if (property.UserId != landlordId)
                return Unauthorized(ApiResponse<object>.UnAuthorized());

            var blockedDate = await _blockedDateRepository.GetByIdAsync(blockedDateId);
            if (blockedDate is null || blockedDate.PropertyId != propertyId)
                return NotFound(ApiResponse<object>.NotFound("BlockedDate"));

            await _blockedDateRepository.DeleteAsync(blockedDate);
            await _blockedDateRepository.SaveChangesAsync();

            return Ok(ApiResponse<object>.Ok("Blocked date deleted successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting blocked date {BlockedDateId}", blockedDateId);
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}

public record BlockDateRequest(DateTime StartDate, DateTime EndDate, string? Reason);
