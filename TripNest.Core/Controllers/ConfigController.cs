using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TripNest.Core.DTOs.Config;
using TripNest.Core.Response;

namespace TripNest.Core.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(IConfiguration configuration, ILogger<ConfigController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("app-info")]
    [ProducesResponseType(typeof(ApiResponse<AppConfigResponse>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<AppConfigResponse>> GetAppInfo()
    {
        try
        {
            var config = new AppConfigResponse
            {
                AppName = "TripNest",
                StayTypes = new[] { "ShortTerm", "LongTerm", "Student" },
                ServiceTypes = new[] { "Cleaning", "Plumbing", "Electrical", "GeneralMaintenance", "Other" },
                MaintenanceCategories = new[] { "Plumbing", "Electrical", "Structural", "Appliance", "Other" },
                Map = new MapConfig
                {
                    Provider = _configuration["Map:Provider"] ?? "OpenStreetMap",
                    TileUrl = _configuration["Map:TileUrl"] ?? "https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",
                    Attribution = _configuration["Map:Attribution"] ?? "© OpenStreetMap contributors",
                    MaxZoom = _configuration.GetValue<int>("Map:MaxZoom", 19)
                }
            };

            return Ok(ApiResponse<AppConfigResponse>.Ok("App config retrieved", config));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving app config");
            return StatusCode(500, ApiResponse<object>.InternalServerError());
        }
    }
}
