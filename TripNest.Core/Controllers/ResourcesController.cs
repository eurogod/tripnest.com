using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/resources")]
[Produces("application/json")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;

    public ResourcesController(IResourceService resourceService) => _resourceService = resourceService;

    /// <summary>List host resources (guides, policies, templates, videos).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ResourceResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<ResourceResponse>>>> GetAll()
    {
        var resources = await _resourceService.GetAllAsync();
        return Ok(ApiResponse<List<ResourceResponse>>.Ok("Resources retrieved", resources));
    }

    /// <summary>Add a resource to the library (admin only).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<ResourceResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<ResourceResponse>>> Create([FromBody] CreateResourceRequest request)
    {
        var resource = await _resourceService.CreateAsync(request);
        return Created($"api/resources/{resource.Id}", ApiResponse<ResourceResponse>.Created("Resource", resource));
    }
}
