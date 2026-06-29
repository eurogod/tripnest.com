using System.Globalization;
using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class PricingService : IPricingService
{
    private readonly IRepository<PricingSettings> _pricingRepository;
    private readonly IPropertyRepository _propertyRepository;

    public PricingService(IRepository<PricingSettings> pricingRepository, IPropertyRepository propertyRepository)
    {
        _pricingRepository = pricingRepository;
        _propertyRepository = propertyRepository;
    }

    public async Task<PricingSettingsResponse> GetAsync(string propertyId, string landlordId)
    {
        var property = await LoadOwnedPropertyAsync(propertyId, landlordId);
        var settings = (await _pricingRepository.FindAsync(p => p.PropertyId == propertyId)).FirstOrDefault();
        return settings is null ? DefaultFor(property) : Map(settings);
    }

    public async Task<PricingSettingsResponse> UpdateAsync(string propertyId, UpdatePricingSettingsRequest request, string landlordId)
    {
        await LoadOwnedPropertyAsync(propertyId, landlordId);

        var settings = (await _pricingRepository.FindAsync(p => p.PropertyId == propertyId)).FirstOrDefault();
        if (settings is null)
        {
            settings = new PricingSettings { PropertyId = propertyId };
            Apply(settings, request);
            await _pricingRepository.AddAsync(settings);
        }
        else
        {
            Apply(settings, request);
            await _pricingRepository.UpdateAsync(settings);
        }

        await _pricingRepository.SaveChangesAsync();
        return Map(settings);
    }

    private async Task<Property> LoadOwnedPropertyAsync(string propertyId, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not own this listing");
        return property;
    }

    private static void Apply(PricingSettings s, UpdatePricingSettingsRequest r)
    {
        s.BaseRate = r.BaseRate;
        s.WeekendRate = r.WeekendRate;
        s.WeeklyDiscountPercent = r.WeeklyDiscountPercent;
        s.MonthlyDiscountPercent = r.MonthlyDiscountPercent;
        s.MinNights = r.MinNights < 1 ? 1 : r.MinNights;
        s.CleaningFee = r.CleaningFee;
        s.UpdatedAt = DateTime.UtcNow;
    }

    private static PricingSettingsResponse Map(PricingSettings s) => new()
    {
        PropertyId = s.PropertyId,
        BaseRate = s.BaseRate,
        WeekendRate = s.WeekendRate,
        WeeklyDiscountPercent = s.WeeklyDiscountPercent,
        MonthlyDiscountPercent = s.MonthlyDiscountPercent,
        MinNights = s.MinNights,
        CleaningFee = s.CleaningFee
    };

    // A sensible starting point derived from the listing itself when no rules are saved yet.
    private static PricingSettingsResponse DefaultFor(Property p)
    {
        var nightly = p.DailyRate ?? Math.Round(p.MonthlyRent / 30m, 2);
        return new PricingSettingsResponse
        {
            PropertyId = p.Id,
            BaseRate = nightly,
            WeekendRate = Math.Round(nightly * 1.15m, 2),
            WeeklyDiscountPercent = 0,
            MonthlyDiscountPercent = 0,
            MinNights = 1,
            CleaningFee = 0
        };
    }
}

public class CalendarService : ICalendarService
{
    private readonly IRepository<PricingSettings> _pricingRepository;
    private readonly IRepository<PropertyBlockedDate> _blockedRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPropertyRepository _propertyRepository;

    public CalendarService(
        IRepository<PricingSettings> pricingRepository,
        IRepository<PropertyBlockedDate> blockedRepository,
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository)
    {
        _pricingRepository = pricingRepository;
        _blockedRepository = blockedRepository;
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
    }

    public async Task<CalendarMonthResponse> GetMonthAsync(string propertyId, int year, int month, string landlordId)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId)
            throw new ForbiddenException("You do not own this listing");

        if (month < 1 || month > 12)
            throw new ValidationException("Month must be between 1 and 12");

        var settings = (await _pricingRepository.FindAsync(p => p.PropertyId == propertyId)).FirstOrDefault();
        var baseRate = settings?.BaseRate ?? property.DailyRate ?? Math.Round(property.MonthlyRent / 30m, 2);
        var weekendRate = settings?.WeekendRate is > 0 ? settings!.WeekendRate : baseRate;
        var minNights = settings?.MinNights ?? 1;

        var blocked = (await _blockedRepository.FindAsync(b => b.PropertyId == propertyId)).ToList();
        var bookings = (await _bookingRepository.GetByPropertyIdAsync(propertyId))
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToList();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var response = new CalendarMonthResponse
        {
            PropertyId = propertyId,
            Year = year,
            Month = month,
            Label = new DateTime(year, month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            MinNights = minNights
        };

        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            var cover = blocked.FirstOrDefault(b => date.Date >= b.StartDate.Date && date.Date <= b.EndDate.Date);
            var isMaintenance = cover?.Reason?.Contains("maintenance", StringComparison.OrdinalIgnoreCase) ?? false;
            var isOwnerBlocked = cover is not null && !isMaintenance;
            var isBooked = bookings.Any(b => date.Date >= b.CheckInDate.Date && date.Date < b.CheckOutDate.Date);

            response.Days.Add(new CalendarDayResponse
            {
                Date = date,
                Price = isWeekend ? weekendRate : baseRate,
                IsWeekend = isWeekend,
                IsDiscounted = false,
                IsOwnerBlocked = isOwnerBlocked,
                IsMaintenance = isMaintenance,
                IsBooked = isBooked
            });
        }

        return response;
    }
}
