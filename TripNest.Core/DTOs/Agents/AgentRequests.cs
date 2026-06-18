namespace TripNest.Core.DTOs.Agents;

public class CreateViewingRequestRequest
{
    public required string PropertyId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Notes { get; set; }
}

public class UpdateViewingRequestStatusRequest
{
    public required string Status { get; set; }
}
