using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.DTOs.Search;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;
using TripNest.Core.Services;

namespace TripNest.Core.Repositories;

public class PropertyRepository : Repository<Property>, IPropertyRepository
{
    public PropertyRepository(AppDbContext context) : base(context)
    {
    }

    // Photos travel with the property everywhere a listing is shown (cards, gallery,
    // cover). FindAsync can't Include, so read by key with the navigation loaded.
    public override async Task<Property?> GetByIdAsync(string id)
    {
        return await _context.Set<Property>()
            .Include(p => p.Photos)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Property>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Property>()
            .Include(p => p.Photos)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Property>> GetAllActiveAsync()
    {
        return await _context.Set<Property>()
            .Include(p => p.Photos)
            .Where(p => p.Status == Enums.PropertyStatus.Active)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Property> Items, int TotalCount)> SearchPageAsync(
        PropertySearchCriteria criteria, int page, int pageSize)
    {
        var query = _context.Set<Property>()
            .AsNoTracking()
            .Include(p => p.Photos)
            .Where(p => p.Status == Enums.PropertyStatus.Active);

        // ToLower().Contains translates to a case-insensitive LIKE on Postgres, served by the
        // trigram (pg_trgm) expression index on lower(Location) — and it also evaluates correctly
        // under the in-memory test provider. Paged in the database: listings grow without bound.
        if (!string.IsNullOrWhiteSpace(criteria.Location))
        {
            var lowered = criteria.Location.ToLower();
            query = query.Where(p => p.Location.ToLower().Contains(lowered));
        }

        if (criteria.MinBedrooms is { } minBeds)
            query = query.Where(p => p.Bedrooms >= minBeds);
        if (criteria.MaxBedrooms is { } maxBeds)
            query = query.Where(p => p.Bedrooms <= maxBeds);

        if (criteria.StayType is { } stayType)
            query = query.Where(p => p.StayType == stayType);

        if (!string.IsNullOrWhiteSpace(criteria.PropertyType))
        {
            var type = criteria.PropertyType.ToLower();
            query = query.Where(p => p.PropertyType.ToLower() == type);
        }

        // Every requested amenity token must appear in the listing's CSV (case-insensitive LIKE
        // per token — amenity names don't collide as substrings in practice).
        if (!string.IsNullOrWhiteSpace(criteria.Amenities))
        {
            foreach (var raw in criteria.Amenities.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var token = raw.ToLower();
                query = query.Where(p => p.Amenities != null && p.Amenities.ToLower().Contains(token));
            }
        }

        // Price bounds compare the effective nightly rate: the daily rate when set, else the
        // pro-rated monthly rent — the same fallback booking pricing uses.
        if (criteria.MinPrice is { } minPrice)
            query = query.Where(p =>
                (p.DailyRate ?? p.MonthlyRent / StayPricingCalculator.ProRataDaysPerMonth) >= minPrice);
        if (criteria.MaxPrice is { } maxPrice)
            query = query.Where(p =>
                (p.DailyRate ?? p.MonthlyRent / StayPricingCalculator.ProRataDaysPerMonth) <= maxPrice);

        // Map viewport: the client sends its visible bounds as it pans/zooms/draws.
        if (criteria.HasBounds)
            query = query.Where(p =>
                p.Latitude >= criteria.MinLat && p.Latitude <= criteria.MaxLat &&
                p.Longitude >= criteria.MinLng && p.Longitude <= criteria.MaxLng);

        // Date availability: exclude listings with an overlapping active booking or blocked range.
        // Same overlap semantics as AvailabilityService.IsRangeAvailable, pushed into the query so
        // unavailable listings never surface (instead of failing later at booking time).
        if (criteria.HasDates)
        {
            var checkIn = criteria.CheckIn!.Value.Date;
            var checkOut = criteria.CheckOut!.Value.Date;
            query = query.Where(p =>
                !_context.Set<Booking>().Any(b =>
                    b.PropertyId == p.Id &&
                    b.Status == Enums.BookingStatus.Confirmed &&
                    b.CheckInDate < checkOut && checkIn < b.CheckOutDate) &&
                !_context.Set<PropertyBlockedDate>().Any(d =>
                    d.PropertyId == p.Id &&
                    d.StartDate < checkOut && checkIn < d.EndDate));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }
}
