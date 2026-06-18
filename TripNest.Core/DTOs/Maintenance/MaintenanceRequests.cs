namespace TripNest.Core.DTOs.Maintenance;

public class CreateMaintenanceRequest
{
    public required string PropertyId { get; set; }
    public required string Category { get; set; }
    public required string Description { get; set; }
    public List<string>? PhotoPaths { get; set; }
    public required string Priority { get; set; }
}

public class UpdateMaintenanceStatusRequest
{
    public required string Status { get; set; }
}

public class ConvertToServiceRequestRequest
{
    public string? CaretakerId { get; set; }
}
