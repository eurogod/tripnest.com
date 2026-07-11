namespace TripNest.Core.Models;

/// <summary>
/// A landlord assigning a caretaker to one of their properties (distinct from ad-hoc
/// tenant-hired service requests). This table — not <c>Caretaker.PropertyId</c> — is the
/// source of truth for which properties a caretaker currently serves; a caretaker can hold
/// several active assignments at once.
/// </summary>
public class PropertyCaretakerAssignment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string CaretakerId { get; set; }
    public Caretaker? Caretaker { get; set; }
    public required string AssignedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
