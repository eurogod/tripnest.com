using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Sweeps group bookings whose split-payment window elapsed: cancels the booking and refunds
/// whoever already paid, so nobody's money sits against a stay that will never confirm. Runs
/// hourly (first pass one interval after startup so tests and boot never race a sweep); paying
/// or initiating a share on an expired booking also triggers the same expiry lazily.
/// </summary>
public class SplitBookingExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SplitBookingExpiryWorker> _logger;

    public SplitBookingExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<SplitBookingExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ISplitBillingService>();
                    await service.ExpireOverdueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Split-booking expiry sweep failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
