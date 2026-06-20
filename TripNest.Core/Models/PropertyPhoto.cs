namespace TripNest.Core.Models;

public class PropertyPhoto
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string PhotoPath { get; set; }
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
