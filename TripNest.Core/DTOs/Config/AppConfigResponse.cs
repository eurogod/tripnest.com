namespace TripNest.Core.DTOs.Config;

public class AppConfigResponse
{
    public required string AppName { get; set; }
    public required string[] StayTypes { get; set; }
    public required string[] ServiceTypes { get; set; }
    public required string[] MaintenanceCategories { get; set; }
    public required MapConfig Map { get; set; }
}

/// <summary>
/// Map tile configuration for the frontend. TripNest uses Leaflet.js with OpenStreetMap
/// tiles, which require no API key (unlike the previous Mapbox setup).
/// </summary>
public class MapConfig
{
    public required string Provider { get; set; }
    public required string TileUrl { get; set; }
    public required string Attribution { get; set; }
    public int MaxZoom { get; set; }
}
