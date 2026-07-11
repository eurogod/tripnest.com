using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Search;

/// <summary>
/// Filters for property search. Everything is optional — an empty criteria matches all Active
/// listings. Dates additionally exclude listings with an overlapping confirmed stay or blocked
/// range, and unlock the per-listing total-stay quote in the results.
/// </summary>
public class PropertySearchCriteria
{
    public string? Location { get; set; }
    public int? MinBedrooms { get; set; }
    public int? MaxBedrooms { get; set; }
    public StayType? StayType { get; set; }
    public string? PropertyType { get; set; }
    /// <summary>Comma-separated amenity tokens; a listing must contain every one (case-insensitive).</summary>
    public string? Amenities { get; set; }
    /// <summary>Nightly-rate bounds (falls back to pro-rated monthly rent when no daily rate).</summary>
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    /// <summary>Map viewport (all four required to apply): south-west / north-east corners.</summary>
    public double? MinLat { get; set; }
    public double? MaxLat { get; set; }
    public double? MinLng { get; set; }
    public double? MaxLng { get; set; }
    /// <summary>Stay dates — filters to available listings and adds a total-cost quote per result.</summary>
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }

    public bool HasBounds => MinLat.HasValue && MaxLat.HasValue && MinLng.HasValue && MaxLng.HasValue;
    public bool HasDates => CheckIn.HasValue && CheckOut.HasValue && CheckOut > CheckIn;
}
