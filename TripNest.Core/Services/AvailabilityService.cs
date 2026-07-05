using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// Single source of truth for property availability: a range is unavailable if it overlaps a
/// Confirmed booking or a landlord-blocked date range.
/// </summary>
public class AvailabilityService : IAvailabilityService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<PropertyBlockedDate> _blockedDateRepository;

    public AvailabilityService(
        IBookingRepository bookingRepository,
        IRepository<PropertyBlockedDate> blockedDateRepository)
    {
        _bookingRepository = bookingRepository;
        _blockedDateRepository = blockedDateRepository;
    }

    public async Task<bool> IsRangeAvailable(string propertyId, DateTime checkIn, DateTime checkOut)
    {
        var bookings = await _bookingRepository.GetByPropertyIdAsync(propertyId);
        var bookingOverlap = bookings.Any(b =>
            b.Status == BookingStatus.Confirmed &&
            b.CheckInDate < checkOut && b.CheckOutDate > checkIn);
        if (bookingOverlap)
            return false;

        var blocked = await _blockedDateRepository.FindAsync(d => d.PropertyId == propertyId);
        var blockedOverlap = blocked.Any(d => d.StartDate < checkOut && d.EndDate > checkIn);

        return !blockedOverlap;
    }

    public async Task<List<DateRange>> GetAvailableRanges(string propertyId, DateTime from, DateTime to)
    {
        var bookings = (await _bookingRepository.GetByPropertyIdAsync(propertyId))
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Select(b => (b.CheckInDate.Date, b.CheckOutDate.Date));
        var blocked = (await _blockedDateRepository.FindAsync(d => d.PropertyId == propertyId))
            .Select(d => (d.StartDate.Date, d.EndDate.Date));
        var unavailable = bookings.Concat(blocked).ToList();

        // Walk day-by-day across [from, to], coalescing consecutive free days into open ranges.
        var ranges = new List<DateRange>();
        DateTime? openStart = null;
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var isFree = !unavailable.Any(u => day >= u.Item1 && day < u.Item2);
            if (isFree && openStart == null)
                openStart = day;
            else if (!isFree && openStart != null)
            {
                ranges.Add(new DateRange(openStart.Value, day));
                openStart = null;
            }
        }
        if (openStart != null)
            ranges.Add(new DateRange(openStart.Value, to.Date.AddDays(1)));

        return ranges;
    }
}
