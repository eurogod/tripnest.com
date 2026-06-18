namespace TripNest.Core.DTOs.Caretakers;

public class ServiceRequestResponse
{
    public required string ServiceRequestId { get; set; }
    public required string CaretakerId { get; set; }
    public required string RequestedByUserId { get; set; }
    public required string PropertyId { get; set; }
    public required string ServiceType { get; set; }
    public required string Description { get; set; }
    public required string Status { get; set; }
    public int? Rating { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
