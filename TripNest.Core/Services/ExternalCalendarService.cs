using TripNest.Core.DTOs.Marketplace;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

/// <summary>Fetches feeds over HTTP via the named factory client (timeout configured at registration).</summary>
public class HttpIcalFeedFetcher : IIcalFeedFetcher
{
    public const string ClientName = "ical-import";
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpIcalFeedFetcher(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public async Task<string> FetchAsync(string url, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        using var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public class ExternalCalendarService : IExternalCalendarService
{
    private readonly IRepository<ExternalCalendar> _calendarRepository;
    private readonly IRepository<PropertyBlockedDate> _blockedRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IIcalFeedFetcher _fetcher;
    private readonly ILogger<ExternalCalendarService> _logger;

    public ExternalCalendarService(
        IRepository<ExternalCalendar> calendarRepository,
        IRepository<PropertyBlockedDate> blockedRepository,
        IPropertyRepository propertyRepository,
        IIcalFeedFetcher fetcher,
        ILogger<ExternalCalendarService> logger)
    {
        _calendarRepository = calendarRepository;
        _blockedRepository = blockedRepository;
        _propertyRepository = propertyRepository;
        _fetcher = fetcher;
        _logger = logger;
    }

    public async Task<ExternalCalendarResponse> AddAsync(string propertyId, string name, string feedUrl, string userId, bool isAdmin)
    {
        await LoadOwnedPropertyAsync(propertyId, userId, isAdmin);

        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("A calendar name is required (e.g. Airbnb)");

        // The server fetches this URL, so gate what it may point at: only http(s), and never
        // loopback/private hosts — a hostile URL must not become a probe of internal services.
        if (!Uri.TryCreate(feedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new ValidationException("Feed URL must be an absolute http(s) address");
        if (uri.IsLoopback || uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
            throw new ValidationException("Feed URL must use a public hostname");

        var calendar = new ExternalCalendar { PropertyId = propertyId, Name = name.Trim(), FeedUrl = feedUrl };
        await _calendarRepository.AddAsync(calendar);
        await _calendarRepository.SaveChangesAsync();

        _logger.LogInformation("External calendar {CalendarId} ({Name}) linked to property {PropertyId}",
            calendar.Id, calendar.Name, propertyId);
        return await MapAsync(calendar);
    }

    public async Task<List<ExternalCalendarResponse>> GetForPropertyAsync(string propertyId, string userId, bool isAdmin)
    {
        await LoadOwnedPropertyAsync(propertyId, userId, isAdmin);
        var calendars = await _calendarRepository.FindAsync(c => c.PropertyId == propertyId);
        var responses = new List<ExternalCalendarResponse>();
        foreach (var calendar in calendars)
            responses.Add(await MapAsync(calendar));
        return responses;
    }

    public async Task RemoveAsync(string externalCalendarId, string userId, bool isAdmin)
    {
        var calendar = await LoadOwnedCalendarAsync(externalCalendarId, userId, isAdmin);

        // The in-memory test provider doesn't honour cascade deletes on optional FKs,
        // and being explicit costs one query — remove this feed's imported rows directly.
        foreach (var range in await _blockedRepository.FindAsync(d => d.ExternalCalendarId == calendar.Id))
            await _blockedRepository.DeleteAsync(range);
        await _calendarRepository.DeleteAsync(calendar);
        await _calendarRepository.SaveChangesAsync();
    }

    public async Task<ExternalCalendarResponse> SyncAsync(string externalCalendarId, string userId, bool isAdmin)
    {
        var calendar = await LoadOwnedCalendarAsync(externalCalendarId, userId, isAdmin);
        await SyncCalendarAsync(calendar, CancellationToken.None);
        await _calendarRepository.SaveChangesAsync();

        // A failed fetch is reported on the row (lastSyncError), not thrown — linking a feed whose
        // host is momentarily down should still succeed, and the worker will retry on schedule.
        return await MapAsync(calendar);
    }

    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var calendars = (await _calendarRepository.GetAllAsync()).ToList();
        foreach (var calendar in calendars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SyncCalendarAsync(calendar, cancellationToken);
        }
        await _calendarRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Full-refresh sync: fetch, parse, then replace this feed's imported rows in one save.
    /// Failures are recorded on the calendar (LastSyncError) and leave the previous import
    /// intact — a flaky feed must not wipe availability blocks that were correct yesterday.
    /// </summary>
    private async Task SyncCalendarAsync(ExternalCalendar calendar, CancellationToken cancellationToken)
    {
        try
        {
            // The FK requires a real user — attribute imported blocks to the listing's owner.
            var owner = (await _propertyRepository.GetByIdAsync(calendar.PropertyId))?.UserId
                ?? throw new InvalidOperationException("The linked property no longer exists");

            var ics = await _fetcher.FetchAsync(calendar.FeedUrl, cancellationToken);
            var ranges = IcalParser.ParseBusyRanges(ics);

            var existing = await _blockedRepository.FindAsync(d => d.ExternalCalendarId == calendar.Id);
            foreach (var stale in existing)
                await _blockedRepository.DeleteAsync(stale);

            foreach (var (start, end) in ranges)
                await _blockedRepository.AddAsync(new PropertyBlockedDate
                {
                    PropertyId = calendar.PropertyId,
                    BlockedByUserId = owner,
                    StartDate = DateTime.SpecifyKind(start, DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(end, DateTimeKind.Utc),
                    Reason = $"Imported from {calendar.Name}",
                    ExternalCalendarId = calendar.Id
                });

            calendar.LastSyncedAt = DateTime.UtcNow;
            calendar.LastSyncError = null;
            await _calendarRepository.UpdateAsync(calendar);
            _logger.LogInformation("Synced external calendar {CalendarId} ({Name}): {Count} busy ranges",
                calendar.Id, calendar.Name, ranges.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Deliberate catch-all: one broken feed (DNS, 404, garbage content) must not stop
            // the worker loop or clobber other feeds. The error is surfaced on the calendar row.
            calendar.LastSyncError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            await _calendarRepository.UpdateAsync(calendar);
            _logger.LogWarning(ex, "External calendar sync failed for {CalendarId} ({Url})",
                calendar.Id, calendar.FeedUrl);
        }
    }

    private async Task LoadOwnedPropertyAsync(string propertyId, string userId, bool isAdmin)
    {
        var property = await _propertyRepository.GetByIdAsync(propertyId)
            ?? throw new NotFoundException("Property");
        if (property.UserId != userId && !isAdmin)
            throw new ForbiddenException("You do not own this listing");
    }

    private async Task<ExternalCalendar> LoadOwnedCalendarAsync(string externalCalendarId, string userId, bool isAdmin)
    {
        // GetByIdAsync (DbSet.FindAsync) honours the identity map, so add-then-sync within one
        // request reuses the tracked instance instead of attaching a duplicate key.
        var calendar = await _calendarRepository.GetByIdAsync(externalCalendarId)
            ?? throw new NotFoundException("External calendar");
        await LoadOwnedPropertyAsync(calendar.PropertyId, userId, isAdmin);
        return calendar;
    }

    private async Task<ExternalCalendarResponse> MapAsync(ExternalCalendar calendar) => new()
    {
        Id = calendar.Id,
        PropertyId = calendar.PropertyId,
        Name = calendar.Name,
        FeedUrl = calendar.FeedUrl,
        LastSyncedAt = calendar.LastSyncedAt,
        LastSyncError = calendar.LastSyncError,
        ImportedRanges = (await _blockedRepository.FindAsync(d => d.ExternalCalendarId == calendar.Id)).Count()
    };
}
