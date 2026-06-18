namespace TripNest.Core.DTOs.Config;

public class AppConfigResponse
{
    public required string AppName { get; set; }
    public required string[] StayTypes { get; set; }
    public required string[] ServiceTypes { get; set; }
    public required string[] MaintenanceCategories { get; set; }
}
