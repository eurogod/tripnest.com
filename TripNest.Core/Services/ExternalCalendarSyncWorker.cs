using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Periodically re-imports every linked external iCal feed so stays booked on other platforms
/// keep blocking dates here without the host lifting a finger. Interval comes from
/// <c>Calendar:ExternalSyncMinutes</c> (0 disables the worker; hosts can still sync manually).
/// The first pass runs one interval after startup, not immediately — boot stays fast and tests
/// never race a background import.
/// </summary>
public class ExternalCalendarSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExternalCalendarSyncWorker> _logger;

    public ExternalCalendarSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ExternalCalendarSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = _configuration.GetValue("Calendar:ExternalSyncMinutes", 60);
        if (minutes <= 0)
        {
            _logger.LogInformation("External calendar sync worker disabled (Calendar:ExternalSyncMinutes <= 0)");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IExternalCalendarService>();
                    await service.SyncAllAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Per-feed failures are already recorded on their rows; this guards the loop
                    // itself (e.g. a transient DB outage) so one bad pass doesn't kill the worker.
                    _logger.LogError(ex, "External calendar sync pass failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
