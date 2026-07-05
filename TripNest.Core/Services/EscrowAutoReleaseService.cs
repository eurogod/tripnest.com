using TripNest.Core.Context;
using TripNest.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace TripNest.Core.Services;

/// <summary>
/// Background service that auto-releases held escrows past grace period with no disputes.
/// Runs once daily at 2 AM UTC.
/// </summary>
public class EscrowAutoReleaseService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscrowAutoReleaseService> _logger;
    private readonly IConfiguration _configuration;

    public EscrowAutoReleaseService(
        IServiceProvider serviceProvider,
        ILogger<EscrowAutoReleaseService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Catch-up pass shortly after startup so eligible escrows aren't stranded when an instance
        // restarts before the next 02:00 window (the old schedule could defer releases up to ~24h).
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await ProcessAutoReleasesAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("EscrowAutoReleaseService cancelled");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in EscrowAutoReleaseService startup pass");
        }

        // Then run once daily at 2 AM UTC.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(2);
                var delay = nextRun - now;

                if (delay.TotalMilliseconds > 0)
                {
                    await Task.Delay(delay, stoppingToken);
                }

                await ProcessAutoReleasesAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("EscrowAutoReleaseService cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EscrowAutoReleaseService");
                // Continue running despite errors
            }
        }
    }

    private async Task ProcessAutoReleasesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var gracePeriodHours = int.Parse(_configuration["Escrow:GracePeriodHours"] ?? "24");
            var cutoffTime = DateTime.UtcNow.AddHours(-gracePeriodHours);

            // Funds are held when the tenant PAYS — often days or weeks before the stay. Releasing
            // on HeldAt alone would hand the landlord the money before check-in even happens,
            // gutting the tenant's protection. Eligibility therefore requires BOTH: the stay has
            // ended (checkout at least the grace period ago, leaving a dispute window) AND the
            // funds have been held at least that long. HeldAt alone is the fallback only for
            // orphaned escrows with no booking row.
            var escrowsToRelease = await context.Escrows
                .Where(e => e.Status == EscrowStatus.HeldInEscrow &&
                           e.HeldAt != null &&
                           e.HeldAt < cutoffTime &&
                           (e.Booking == null || e.Booking.CheckOutDate < cutoffTime))
                .Include(e => e.Booking)
                    .ThenInclude(b => b!.Property)
                .ToListAsync(cancellationToken);

            foreach (var escrow in escrowsToRelease)
            {
                try
                {
                    escrow.Status = EscrowStatus.Released;
                    escrow.ReleasedAt = DateTime.UtcNow;
                    escrow.ReleaseReason = "Auto-released after grace period";

                    // Update linked booking if exists
                    if (escrow.Booking != null)
                    {
                        escrow.Booking.Status = BookingStatus.Completed;
                    }

                    _logger.LogInformation("Auto-released escrow {EscrowId} (booking: {BookingId})",
                        escrow.Id, escrow.BookingId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error auto-releasing escrow {EscrowId}", escrow.Id);
                }
            }

            if (escrowsToRelease.Count > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Auto-released {EscrowCount} escrows", escrowsToRelease.Count);

                // Disburse each released escrow to its host, exactly like a manual release.
                // CreateForReleasedEscrowAsync is idempotent and never throws for provider issues.
                var payoutService = scope.ServiceProvider.GetRequiredService<TripNest.Core.Interfaces.Services.IPayoutService>();
                foreach (var escrow in escrowsToRelease)
                {
                    var landlordId = escrow.Booking?.Property?.UserId;
                    if (landlordId is not null)
                        await payoutService.CreateForReleasedEscrowAsync(escrow, landlordId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing escrow auto-releases");
        }
    }
}
