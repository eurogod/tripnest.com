namespace TripNest.Core.DTOs.Search;

public class GlobalSearchResultDto
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Title { get; set; }
    public required string Subtitle { get; set; }
    public string? ThumbnailUrl { get; set; }
}
