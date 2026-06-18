namespace TripNest.Core.DTOs.Properties;

public class CreateWalkthroughRequest
{
    public required string PropertyId { get; set; }
    public required string Title { get; set; }
    public required string VideoUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int DurationSeconds { get; set; }
}
