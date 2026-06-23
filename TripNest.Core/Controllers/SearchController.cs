using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using TripNest.Core.DTOs.Search;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IPropertyRepository _propertyRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly ICaretakerRepository _caretakerRepository;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        IPropertyRepository propertyRepository,
        IAgentRepository agentRepository,
        ICaretakerRepository caretakerRepository,
        ILogger<SearchController> logger)
    {
        _propertyRepository = propertyRepository;
        _agentRepository = agentRepository;
        _caretakerRepository = caretakerRepository;
        _logger = logger;
    }

    [HttpGet]
    [OutputCache(PolicyName = "listings")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<GlobalSearchResultDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IEnumerable<GlobalSearchResultDto>>>> Search([FromQuery] string? q = null, [FromQuery] string? type = null)
    {
        try
        {
            var results = new List<GlobalSearchResultDto>();

            if (string.IsNullOrEmpty(type) || type == "properties")
            {
                var properties = await _propertyRepository.GetAllAsync();
                var filtered = string.IsNullOrEmpty(q)
                    ? properties
                    : properties.Where(p => p.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(q, StringComparison.OrdinalIgnoreCase));

                results.AddRange(filtered.Take(10).Select(p => new GlobalSearchResultDto
                {
                    Id = p.Id,
                    Type = "property",
                    Title = p.Title,
                    Subtitle = p.Location,
                    ThumbnailUrl = null
                }));
            }

            if (string.IsNullOrEmpty(type) || type == "agents")
            {
                var agents = await _agentRepository.GetAllAsync();
                var filtered = string.IsNullOrEmpty(q)
                    ? agents
                    : agents.Where(a => a.User?.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);

                results.AddRange(filtered.Take(10).Select(a => new GlobalSearchResultDto
                {
                    Id = a.Id,
                    Type = "agent",
                    Title = a.User?.FullName ?? "Unknown",
                    Subtitle = a.LicenseNumber,
                    ThumbnailUrl = a.User?.ProfilePhotoPath
                }));
            }

            if (string.IsNullOrEmpty(type) || type == "caretakers")
            {
                var caretakers = await _caretakerRepository.GetAllAsync();
                var filtered = string.IsNullOrEmpty(q)
                    ? caretakers
                    : caretakers.Where(c => c.User?.FullName.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);

                results.AddRange(filtered.Take(10).Select(c => new GlobalSearchResultDto
                {
                    Id = c.Id,
                    Type = "caretaker",
                    Title = c.User?.FullName ?? "Unknown",
                    Subtitle = c.Responsibilities,
                    ThumbnailUrl = c.User?.ProfilePhotoPath
                }));
            }

            return Ok(ApiResponse<IEnumerable<GlobalSearchResultDto>>.Ok("Search results", results));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
