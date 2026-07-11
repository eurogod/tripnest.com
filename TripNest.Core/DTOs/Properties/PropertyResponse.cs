using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Properties;

public class PropertyResponse
{
    public required string PropertyId { get; set; }
    /// <summary>Listing owner — lets clients start a conversation / attribute inquiries.</summary>
    public string? OwnerId { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
    public required int Bedrooms { get; set; }
    public required int Bathrooms { get; set; }
    public required decimal MonthlyRent { get; set; }
    public decimal? DailyRate { get; set; }
    public required string PropertyType { get; set; }
    public StayType StayType { get; set; }
    public CancellationPolicy CancellationPolicy { get; set; }
    public string? Amenities { get; set; }
    public string? PhotoPaths { get; set; }
    public PropertyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    /// <summary>All-in cost for the searched dates (null when the search carried no dates).</summary>
    public StayQuote? Quote { get; set; }
}
