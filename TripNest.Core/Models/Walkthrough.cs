namespace TripNest.Core.Models;

public class Walkthrough
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string PropertyId { get; set; }
    public Property? Property { get; set; }
    public required string Title { get; set; }
    public required string VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
