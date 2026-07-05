using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class PropertyRepository : Repository<Property>, IPropertyRepository
{
    public PropertyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Property>> GetByUserIdAsync(string userId)
    {
        return await _context.Set<Property>()
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Property>> GetAllActiveAsync()
    {
        return await _context.Set<Property>()
            .Where(p => p.Status == Enums.PropertyStatus.Active)
            .ToListAsync();
    }

    public async Task<(IReadOnlyList<Property> Items, int TotalCount)> SearchPageAsync(
        string location, int minBedrooms, int maxBedrooms, int page, int pageSize)
    {
        // ToLower().Contains translates to a case-insensitive LIKE on Postgres, served by the
        // trigram (pg_trgm) expression index on lower(Location) — and it also evaluates correctly
        // under the in-memory test provider. Paged in the database: listings grow without bound.
        var lowered = location.ToLower();
        var query = _context.Set<Property>()
            .AsNoTracking()
            .Where(p => p.Status == Enums.PropertyStatus.Active &&
                        p.Location.ToLower().Contains(lowered) &&
                        p.Bedrooms >= minBedrooms &&
                        p.Bedrooms <= maxBedrooms);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, totalCount);
    }
}
