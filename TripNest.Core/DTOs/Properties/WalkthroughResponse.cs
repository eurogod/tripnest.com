using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Properties;

public class WalkthroughResponse
{
    public required string WalkthroughId { get; set; }
    public required string PropertyId { get; set; }
    public required string Title { get; set; }
    public required string VideoPath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PropertyWalkthroughStatusResponse
{
    public required string PropertyId { get; set; }
    public WalkthroughStatus WalkthroughStatus { get; set; }
    public string? VideoPath { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class ReviewWalkthroughRequest
{
    public bool Approved { get; set; }
    public string? RejectionReason { get; set; }
}
