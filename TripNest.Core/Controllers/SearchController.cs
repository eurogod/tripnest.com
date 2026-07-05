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

            // ToLower().Contains(...) translates to a case-insensitive SQL LIKE on Postgres and also
            // evaluates correctly under the in-memory test provider (unlike EF.Functions.ILike or the
            // StringComparison overload, which the in-memory provider can't translate).
            var ql = string.IsNullOrWhiteSpace(q) ? null : q.ToLower();

            if (string.IsNullOrEmpty(type) || type == "properties")
            {
                var (properties, _) = await _propertyRepository.FindPageAsync(
                    ql == null ? null : p => p.Title.ToLower().Contains(ql) || p.Description.ToLower().Contains(ql),
                    query => query.OrderByDescending(p => p.CreatedAt),
                    page: 1,
                    pageSize: 10);

                results.AddRange(properties.Select(p => new GlobalSearchResultDto
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
                var agents = await _agentRepository.SearchByNameAsync(q, 10);
                results.AddRange(agents.Select(a => new GlobalSearchResultDto
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
                var caretakers = await _caretakerRepository.SearchByNameAsync(q, 10);
                results.AddRange(caretakers.Select(c => new GlobalSearchResultDto
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
