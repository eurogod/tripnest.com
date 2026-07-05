namespace TripNest.Core.DTOs.Properties;

/// <summary>AI-drafted listing copy for the host to review, edit, and accept — never auto-applied.</summary>
public class ListingCopySuggestion
{
    public required string Title { get; set; }
    public required string Description { get; set; }
    public List<string> Highlights { get; set; } = new();
}
