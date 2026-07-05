using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace TripNest.Core.Services;

/// <summary>
/// Background service that auto-releases held escrows past grace period with no disputes.
/// Runs once daily at 2 AM UTC.
/// </summary>
public class EscrowAutoReleaseService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EscrowAutoReleaseService> _logger;
    private readonly EscrowOptions _escrowOptions;

    public EscrowAutoReleaseService(
        IServiceProvider serviceProvider,
        ILogger<EscrowAutoReleaseService> logger,
        IOptions<EscrowOptions> escrowOptions)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _escrowOptions = escrowOptions.Value;
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
                // Next 02:00 UTC — today's if it hasn't passed yet, otherwise tomorrow's
                // (always targeting tomorrow would skip a run for instances started before 02:00).
                var now = DateTime.UtcNow;
                var todayRun = now.Date.AddHours(2);
                var nextRun = now < todayRun ? todayRun : todayRun.AddDays(1);
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

    // Arbitrary but stable bigint identifying "the escrow auto-release pass" to Postgres advisory
    // locking, so every instance contends for the same lock.
    private const long AutoReleaseAdvisoryLockKey = 727_566_001;

    private async Task ProcessAutoReleasesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            // Every instance runs this schedule; a session-level Postgres advisory lock ensures only
            // one actually performs the pass, so concurrent instances can't race on the same escrows.
            // The connection is opened explicitly so acquire and release happen on the same session
            // (the lock also dies with the session if the instance crashes mid-pass). Skipped on
            // non-relational providers — the test suite's in-memory database has no advisory locks.
            var useAdvisoryLock = context.Database.IsNpgsql();
            if (useAdvisoryLock)
            {
                await context.Database.OpenConnectionAsync(cancellationToken);
                var acquired = (await context.Database
                    .SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({AutoReleaseAdvisoryLockKey}) AS \"Value\"")
                    .ToListAsync(cancellationToken)).Single();
                if (!acquired)
                {
                    _logger.LogInformation("Escrow auto-release pass skipped: another instance holds the lock");
                    return;
                }
            }

            try
            {
                await ReleaseEligibleEscrowsAsync(scope, context, cancellationToken);
            }
            finally
            {
                if (useAdvisoryLock)
                    await context.Database.ExecuteSqlRawAsync(
                        $"SELECT pg_advisory_unlock({AutoReleaseAdvisoryLockKey})", cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing escrow auto-releases");
        }
    }

    private async Task ReleaseEligibleEscrowsAsync(IServiceScope scope, AppDbContext context, CancellationToken cancellationToken)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-_escrowOptions.GracePeriodHours);

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
                context.Set<Models.EscrowEvent>().Add(EscrowStateMachine.Transition(
                    escrow, EscrowStatus.Released, actor: "system:auto-release", reason: "Auto-released after grace period"));
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
}
