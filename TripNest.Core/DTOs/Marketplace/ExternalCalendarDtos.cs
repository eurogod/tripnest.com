namespace TripNest.Core.DTOs.Marketplace;

public class AddExternalCalendarRequest
{
    public required string Name { get; set; }
    public required string FeedUrl { get; set; }
}

public class ExternalCalendarResponse
{
    public required string Id { get; set; }
    public required string PropertyId { get; set; }
    public required string Name { get; set; }
    public required string FeedUrl { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
    /// <summary>Busy ranges currently imported from this feed.</summary>
    public int ImportedRanges { get; set; }
}
