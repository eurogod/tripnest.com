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
}
