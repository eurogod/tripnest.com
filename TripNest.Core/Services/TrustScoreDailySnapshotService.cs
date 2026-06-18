using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace TripNest.Core.Services;

/// <summary>
/// Background service that computes daily trust score snapshots for all properties and users.
/// Runs once daily at 3 AM UTC.
/// </summary>
public class TrustScoreDailySnapshotService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TrustScoreDailySnapshotService> _logger;

    public TrustScoreDailySnapshotService(
        IServiceProvider serviceProvider,
        ILogger<TrustScoreDailySnapshotService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once daily at 3 AM UTC
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(3);
                var delay = nextRun - now;

                if (delay.TotalMilliseconds > 0)
                {
                    await Task.Delay(delay, stoppingToken);
                }

                await GenerateDailySnapshotsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("TrustScoreDailySnapshotService cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TrustScoreDailySnapshotService");
                // Continue running despite errors
            }
        }
    }

    private async Task GenerateDailySnapshotsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trustScoreService = scope.ServiceProvider.GetRequiredService<ITrustScoreService>();

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Generate snapshots for all properties
            var properties = await context.Properties
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var property in properties)
            {
                try
                {
                    await trustScoreService.RecalculateNowAsync("Property", property.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating trust score for property {PropertyId}", property.Id);
                }
            }

            // Generate snapshots for all landlords (users with properties)
            var landlords = await context.Users
                .Where(u => u.Role == UserRole.Landlord && context.Properties.Any(p => p.UserId == u.Id))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var landlord in landlords)
            {
                try
                {
                    await trustScoreService.RecalculateNowAsync("User", landlord.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating trust score for user {UserId}", landlord.Id);
                }
            }

            _logger.LogInformation("Generated daily trust score snapshots for {PropertyCount} properties and {LandlordCount} landlords",
                properties.Count, landlords.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating daily trust score snapshots");
        }
    }
}
