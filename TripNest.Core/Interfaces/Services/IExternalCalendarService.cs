using TripNest.Core.DTOs.Marketplace;

namespace TripNest.Core.Interfaces.Services;

/// <summary>Fetches an external iCal feed's raw text. Swapped for a stub in tests.</summary>
public interface IIcalFeedFetcher
{
    Task<string> FetchAsync(string url, CancellationToken cancellationToken = default);
}

public interface IExternalCalendarService
{
    Task<ExternalCalendarResponse> AddAsync(string propertyId, string name, string feedUrl, string userId, bool isAdmin);
    Task<List<ExternalCalendarResponse>> GetForPropertyAsync(string propertyId, string userId, bool isAdmin);
    Task RemoveAsync(string externalCalendarId, string userId, bool isAdmin);
    /// <summary>Fetches the feed and replaces its imported blocked ranges. Owner-triggered.
    /// Fetch failures are reported via <c>LastSyncError</c> on the response, not thrown.</summary>
    Task<ExternalCalendarResponse> SyncAsync(string externalCalendarId, string userId, bool isAdmin);
    /// <summary>Syncs every linked feed; failures are recorded per-feed, never thrown (worker path).</summary>
    Task SyncAllAsync(CancellationToken cancellationToken = default);
}
