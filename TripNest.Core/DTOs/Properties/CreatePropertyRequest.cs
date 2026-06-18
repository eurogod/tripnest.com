using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Properties;

public class CreatePropertyRequest
{
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
    public string? Amenities { get; set; }
}
