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

    public async Task<IEnumerable<Property>> SearchAsync(string location, int minBedrooms, int maxBedrooms)
    {
        return await _context.Set<Property>()
            .Where(p => p.Status == Enums.PropertyStatus.Active &&
                        p.Location.Contains(location) &&
                        p.Bedrooms >= minBedrooms &&
                        p.Bedrooms <= maxBedrooms)
            .ToListAsync();
    }
}
