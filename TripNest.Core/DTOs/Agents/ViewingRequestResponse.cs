namespace TripNest.Core.DTOs.Agents;

public class ViewingRequestResponse
{
    public required string ViewingRequestId { get; set; }
    public required string AgentId { get; set; }
    public required string TenantId { get; set; }
    public required string PropertyId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public string? Notes { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
