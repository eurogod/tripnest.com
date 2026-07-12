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

    // NOTE: Apply/Map below carry the dynamic-pricing opt-in and rate bounds too.
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
        s.DynamicPricingEnabled = r.DynamicPricingEnabled;
        s.MinNightlyRate = r.MinNightlyRate;
        s.MaxNightlyRate = r.MaxNightlyRate;
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
        CleaningFee = s.CleaningFee,
        DynamicPricingEnabled = s.DynamicPricingEnabled,
        MinNightlyRate = s.MinNightlyRate,
        MaxNightlyRate = s.MaxNightlyRate
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
    private readonly IConfiguration _configuration;

    public CalendarService(
        IRepository<PricingSettings> pricingRepository,
        IRepository<PropertyBlockedDate> blockedRepository,
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IConfiguration configuration)
    {
        _pricingRepository = pricingRepository;
        _blockedRepository = blockedRepository;
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _configuration = configuration;
    }

    public async Task<string> GetIcalFeedPathAsync(string propertyId, string landlordId, bool isAdmin)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != landlordId && !isAdmin)
            throw new ForbiddenException("You do not own this listing");

        return $"/api/calendar/{propertyId}.ics?token={FeedToken(propertyId)}";
    }

    public async Task<string> GetIcalFeedAsync(string propertyId, string token)
    {
        // The token is the only credential on this anonymous endpoint; compare in constant time.
        var expected = FeedToken(propertyId);
        if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(expected),
                System.Text.Encoding.UTF8.GetBytes(token ?? string.Empty)))
            throw new ForbiddenException("Invalid calendar feed token");

        _ = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");

        var bookings = (await _bookingRepository.GetByPropertyIdAsync(propertyId))
            .Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn);
        var blocked = await _blockedRepository.FindAsync(d => d.PropertyId == propertyId);

        // Minimal RFC 5545 document: external platforms only need DTSTART/DTEND busy ranges.
        // DTEND is exclusive, matching checkout-day semantics on every major platform.
        var sb = new System.Text.StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//TripNest//Core//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        foreach (var b in bookings)
            AppendEvent(sb, $"booking-{b.Id}", b.CheckInDate, b.CheckOutDate, "Reserved (TripNest)");
        foreach (var d in blocked)
            AppendEvent(sb, $"blocked-{d.Id}", d.StartDate, d.EndDate, "Not available");
        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static void AppendEvent(System.Text.StringBuilder sb, string uid, DateTime start, DateTime end, string summary)
    {
        sb.Append("BEGIN:VEVENT\r\n");
        sb.Append($"UID:{uid}@tripnest\r\n");
        sb.Append($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}\r\n");
        sb.Append($"DTSTART;VALUE=DATE:{start:yyyyMMdd}\r\n");
        sb.Append($"DTEND;VALUE=DATE:{end:yyyyMMdd}\r\n");
        sb.Append($"SUMMARY:{summary}\r\n");
        sb.Append("END:VEVENT\r\n");
    }

    /// <summary>Deterministic per-property feed secret — HMAC of the property id under the JWT
    /// signing key, so the URL is unguessable without storing anything new.</summary>
    private string FeedToken(string propertyId)
    {
        var key = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured");
        var hash = System.Security.Cryptography.HMACSHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(key),
            System.Text.Encoding.UTF8.GetBytes($"ical-feed:{propertyId}"));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
        // A long-stay (monthly) discount, when configured, is surfaced on weekday nights as the
        // effective discounted nightly rate so the calendar reflects the real price a guest would pay.
        var monthlyDiscount = settings?.MonthlyDiscountPercent ?? 0m;

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

            var price = isWeekend ? weekendRate : baseRate;
            var isDiscounted = false;
            if (!isWeekend && monthlyDiscount > 0)
            {
                price = Math.Round(price * (1 - monthlyDiscount / 100m), 2);
                isDiscounted = true;
            }

            response.Days.Add(new CalendarDayResponse
            {
                Date = date,
                Price = price,
                IsWeekend = isWeekend,
                IsDiscounted = isDiscounted,
                IsOwnerBlocked = isOwnerBlocked,
                IsMaintenance = isMaintenance,
                IsBooked = isBooked
            });
        }

        return response;
    }
}
