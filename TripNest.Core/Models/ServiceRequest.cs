using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class ServiceRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string CaretakerId { get; set; }
    public Caretaker? Caretaker { get; set; }
    public required string RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string ServiceType { get; set; }
    public required string Description { get; set; }
    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;
    public int? Rating { get; set; }
    public string? ReviewComment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
