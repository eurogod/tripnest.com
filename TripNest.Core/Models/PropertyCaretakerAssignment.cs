namespace TripNest.Core.Models;

/// <summary>
/// A landlord permanently assigning a caretaker to one of their properties
/// (distinct from ad-hoc tenant-hired service requests).
/// </summary>
public class PropertyCaretakerAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string CaretakerId { get; set; }
    public User? Caretaker { get; set; }
    public required string AssignedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
