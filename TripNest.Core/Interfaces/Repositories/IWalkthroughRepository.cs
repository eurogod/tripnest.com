using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Repositories;

public interface IWalkthroughRepository : IRepository<Walkthrough>
{
    Task<IEnumerable<Walkthrough>> GetByPropertyIdAsync(string propertyId);

    /// <summary>
    /// Aggregate walkthrough metrics computed in the database (COUNT/DISTINCT/MAX/SUM), so the
    /// dashboard doesn't materialise the whole table. <paramref name="recentSince"/> bounds the
    /// "recent" count.
    /// </summary>
    Task<WalkthroughStats> GetStatsAsync(DateTime recentSince);
}

/// <summary>Aggregate counts/totals across all walkthroughs.</summary>
public readonly record struct WalkthroughStats(
    int Total,
    int DistinctPropertyCount,
    int RecentCount,
    DateTime? LastCreatedAt,
    long TotalDurationSeconds);
