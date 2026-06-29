using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Extensions;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
[Authorize(Roles = "Landlord,Admin")]
public class HostTasksController : ControllerBase
{
    private readonly IHostTaskService _taskService;

    public HostTasksController(IHostTaskService taskService) => _taskService = taskService;

    /// <summary>List the caller's operational tasks.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<HostTaskResponse>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<List<HostTaskResponse>>>> GetMine()
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<List<HostTaskResponse>>.UnAuthorized());

        var tasks = await _taskService.GetMineAsync(landlordId);
        return Ok(ApiResponse<List<HostTaskResponse>>.Ok("Tasks retrieved", tasks));
    }

    /// <summary>Create a task.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<HostTaskResponse>), StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<HostTaskResponse>>> Create([FromBody] CreateHostTaskRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<HostTaskResponse>.UnAuthorized());

        var task = await _taskService.CreateAsync(request, landlordId);
        return Created($"api/tasks/{task.Id}", ApiResponse<HostTaskResponse>.Created("Task", task));
    }

    /// <summary>Update a task (status, assignee, priority, etc.).</summary>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(ApiResponse<HostTaskResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<HostTaskResponse>>> Update(string id, [FromBody] UpdateHostTaskRequest request)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<HostTaskResponse>.UnAuthorized());

        var task = await _taskService.UpdateAsync(id, request, landlordId);
        return Ok(ApiResponse<HostTaskResponse>.Ok("Task updated", task));
    }

    /// <summary>Delete a task.</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id)
    {
        var landlordId = User.GetUserId();
        if (string.IsNullOrEmpty(landlordId))
            return Unauthorized(ApiResponse<object>.UnAuthorized());

        await _taskService.DeleteAsync(id, landlordId);
        return Ok(ApiResponse<object>.Ok("Task deleted"));
    }
}
