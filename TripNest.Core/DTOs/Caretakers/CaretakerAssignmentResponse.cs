namespace TripNest.Core.DTOs.Caretakers;

public class CaretakerAssignmentResponse
{
    public required string AssignmentId { get; set; }
    public required string PropertyId { get; set; }
    public required string CaretakerId { get; set; }
    public required string AssignedByUserId { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? EndedAt { get; set; }
}
