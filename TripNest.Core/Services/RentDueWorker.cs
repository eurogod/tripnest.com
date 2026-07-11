using TripNest.Core.Interfaces.Services;

namespace TripNest.Core.Services;

/// <summary>
/// Daily rent sweep: moves upcoming invoices into Due inside the reminder window
/// (Rent:DueReminderDays) and past-due ones into Overdue, notifying the parties. First pass
/// runs one interval after startup so boot and tests never race a sweep.
/// </summary>
public class RentDueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RentDueWorker> _logger;

    public RentDueWorker(IServiceScopeFactory scopeFactory, ILogger<RentDueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IRentService>();
                    await service.ProcessDueAndOverdueAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Rent due/overdue sweep failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
