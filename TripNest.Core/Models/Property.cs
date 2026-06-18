using TripNest.Core.Enums;

namespace TripNest.Core.Models;

public class Property
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string UserId { get; set; }
    public User? User { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public required double Latitude { get; set; }
    public required double Longitude { get; set; }
    public required int Bedrooms { get; set; }
    public required int Bathrooms { get; set; }
    public required decimal MonthlyRent { get; set; }
    public required decimal? DailyRate { get; set; }
    public required string PropertyType { get; set; }
    public string? Amenities { get; set; }
    public string? PhotoPaths { get; set; }
    public PropertyStatus Status { get; set; } = PropertyStatus.Draft;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Walkthrough> Walkthroughs { get; set; } = new List<Walkthrough>();
}
