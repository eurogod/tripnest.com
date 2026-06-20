namespace TripNest.Core.Interfaces.Services;

public record DateRange(DateTime Start, DateTime End);

public interface IAvailabilityService
{
    /// <summary>True if [checkIn, checkOut) overlaps no Confirmed booking and no blocked-date range.</summary>
    Task<bool> IsRangeAvailable(string propertyId, DateTime checkIn, DateTime checkOut);

    /// <summary>Open (bookable) date ranges within [from, to], for the frontend calendar.</summary>
    Task<List<DateRange>> GetAvailableRanges(string propertyId, DateTime from, DateTime to);
}
