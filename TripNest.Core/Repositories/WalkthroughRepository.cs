using Microsoft.EntityFrameworkCore;
using TripNest.Core.Context;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Models;

namespace TripNest.Core.Repositories;

public class WalkthroughRepository : Repository<Walkthrough>, IWalkthroughRepository
{
    public WalkthroughRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Walkthrough>> GetByPropertyIdAsync(string propertyId)
    {
        return await _context.Set<Walkthrough>()
            .Where(w => w.PropertyId == propertyId)
            .ToListAsync();
    }

    public async Task<WalkthroughStats> GetStatsAsync(DateTime recentSince)
    {
        var set = _context.Set<Walkthrough>().AsNoTracking();

        // Casting Max to DateTime? yields null (not an exception) when there are no rows; Sum over
        // an empty set is 0.
        return new WalkthroughStats(
            Total: await set.CountAsync(),
            DistinctPropertyCount: await set.Select(w => w.PropertyId).Distinct().CountAsync(),
            RecentCount: await set.CountAsync(w => w.CreatedAt > recentSince),
            LastCreatedAt: await set.MaxAsync(w => (DateTime?)w.CreatedAt),
            TotalDurationSeconds: await set.SumAsync(w => (long)w.DurationSeconds));
    }
}
