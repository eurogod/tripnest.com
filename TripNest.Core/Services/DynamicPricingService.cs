using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>
/// Smart dynamic pricing (opt-in per listing). Each night's rate is the base (or weekend) rate
/// times three explainable demand factors, clamped to the host's floor/ceiling:
///
///  • Area occupancy — how booked the listing's city already is that night (0.9× empty → 1.3× full),
///  • Lead time — unsold nights inside the last-minute window are discounted to fill (0.9×),
///  • Demand events — admin-curated festivals/conferences lift matching locations by their uplift.
///
/// Deliberately simple and inspectable: a host can look at any adjusted price and see why. The
/// hyper-local signal comes from the platform's own booking density; events cover what booking
/// data can't see coming.
/// </summary>
public class DynamicPricingService : IDynamicPricingService
{
    private const double OccupancyFloorFactor = 0.9;   // empty city
    private const double OccupancySwing = 0.4;         // fully booked city → 1.3×
    private const int LastMinuteDays = 7;
    private const double LastMinuteFactor = 0.9;
    private const decimal AutoFloorFraction = 0.7m;    // when the host sets no MinNightlyRate
    private const decimal AutoCeilingFraction = 1.5m;  // when the host sets no MaxNightlyRate

    private readonly IPropertyRepository _propertyRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRepository<DemandEvent> _eventRepository;

    public DynamicPricingService(
        IPropertyRepository propertyRepository,
        IBookingRepository bookingRepository,
        IRepository<DemandEvent> eventRepository)
    {
        _propertyRepository = propertyRepository;
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
    }

    public async Task<PricingSettings?> AdjustAsync(Property property, PricingSettings? pricing, DateTime checkIn, DateTime checkOut)
    {
        if (pricing is not { DynamicPricingEnabled: true } || checkOut.Date <= checkIn.Date)
            return pricing;

        var baseRate = pricing.BaseRate > 0
            ? pricing.BaseRate
            : property.DailyRate ?? property.MonthlyRent / StayPricingCalculator.ProRataDaysPerMonth;
        var weekendRate = pricing.WeekendRate > 0 ? pricing.WeekendRate : baseRate;

        var floor = pricing.MinNightlyRate > 0 ? pricing.MinNightlyRate : Math.Round(baseRate * AutoFloorFraction, 2);
        var ceiling = pricing.MaxNightlyRate > 0 ? pricing.MaxNightlyRate : Math.Round(baseRate * AutoCeilingFraction, 2);

        // City = the first comma-separated segment of the location ("Accra, Ghana" → "accra");
        // demand is measured against other active listings in the same city.
        var city = CityOf(property.Location);
        var cityProperties = (await _propertyRepository.GetAllActiveAsync())
            .Where(p => CityOf(p.Location) == city)
            .Select(p => p.Id)
            .ToList();
        var cityBookings = cityProperties.Count == 0
            ? new List<Booking>()
            : (await _bookingRepository.FindAsync(b =>
                cityProperties.Contains(b.PropertyId) &&
                b.Status == BookingStatus.Confirmed &&
                b.CheckInDate < checkOut && b.CheckOutDate > checkIn)).ToList();

        var events = (await _eventRepository.FindAsync(e =>
                e.StartDate < checkOut && e.EndDate > checkIn))
            .Where(e => city.Contains(e.Location.Trim().ToLowerInvariant()) ||
                        property.Location.ToLowerInvariant().Contains(e.Location.Trim().ToLowerInvariant()))
            .ToList();

        decimal weekdayTotal = 0, weekendTotal = 0;
        int weekdayNights = 0, weekendNights = 0;
        for (var night = checkIn.Date; night < checkOut.Date; night = night.AddDays(1))
        {
            var isWeekend = night.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
            var rate = isWeekend ? weekendRate : baseRate;

            // Occupancy: share of the city's active listings already booked this night.
            var occupied = cityBookings.Count(b => b.CheckInDate <= night && night < b.CheckOutDate);
            var occupancy = cityProperties.Count == 0 ? 0 : (double)occupied / cityProperties.Count;
            var factor = OccupancyFloorFactor + OccupancySwing * occupancy;

            // Fill unsold last-minute nights.
            if ((night - DateTime.UtcNow.Date).TotalDays <= LastMinuteDays)
                factor *= LastMinuteFactor;

            // The strongest matching event wins (uplifts don't stack).
            var uplift = events
                .Where(e => e.StartDate <= night && night < e.EndDate)
                .Select(e => e.UpliftPercent)
                .DefaultIfEmpty(0m)
                .Max();
            factor *= 1 + (double)uplift / 100;

            var adjusted = Math.Clamp(Math.Round(rate * (decimal)factor, 2), floor, ceiling);
            if (isWeekend) { weekendTotal += adjusted; weekendNights++; }
            else { weekdayTotal += adjusted; weekdayNights++; }
        }

        // Feed the calculator per-class average rates so the quote breakdown stays consistent
        // (and identical between search, the quote endpoint, and the booking charge).
        return new PricingSettings
        {
            PropertyId = pricing.PropertyId,
            BaseRate = weekdayNights > 0 ? Math.Round(weekdayTotal / weekdayNights, 2) : baseRate,
            WeekendRate = weekendNights > 0 ? Math.Round(weekendTotal / weekendNights, 2) : weekendRate,
            WeeklyDiscountPercent = pricing.WeeklyDiscountPercent,
            MonthlyDiscountPercent = pricing.MonthlyDiscountPercent,
            MinNights = pricing.MinNights,
            CleaningFee = pricing.CleaningFee,
            DynamicPricingEnabled = true,
            MinNightlyRate = pricing.MinNightlyRate,
            MaxNightlyRate = pricing.MaxNightlyRate
        };
    }

    private static string CityOf(string location) =>
        (location ?? "").Split(',')[0].Trim().ToLowerInvariant();
}
