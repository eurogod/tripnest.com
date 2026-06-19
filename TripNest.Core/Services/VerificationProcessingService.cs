using TripNest.Core.Context;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace TripNest.Core.Services;

/// <summary>
/// Hosted background service that drains <see cref="IVerificationQueue"/> and resolves each
/// queued verification (NIA lookup + face match) off the HTTP request path. A fresh DI scope
/// is created per item so the scoped <see cref="VerificationService"/> / repositories / DbContext
/// behave exactly as in a request. On startup it re-enqueues any rows still Pending so work
/// survives a restart mid-processing.
/// </summary>
public class VerificationProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IVerificationQueue _queue;
    private readonly ILogger<VerificationProcessingService> _logger;

    public VerificationProcessingService(
        IServiceProvider serviceProvider,
        IVerificationQueue queue,
        ILogger<VerificationProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            string verificationId;
            try
            {
                verificationId = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessOneAsync(verificationId);
        }
    }

    private async Task ProcessOneAsync(string verificationId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var verificationService = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            await verificationService.ProcessVerificationAsync(verificationId);
        }
        catch (Exception ex)
        {
            // ProcessVerificationAsync already persists failure state; this guards the loop itself.
            _logger.LogError(ex, "Unhandled error processing verification {VerificationId}", verificationId);
        }
    }

    /// <summary>Re-enqueues verifications left Pending by a previous run (e.g. crash/restart).</summary>
    private async Task RequeuePendingAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pendingIds = await db.VerificationRequests
                .Where(v => v.Status == VerificationStatus.Pending)
                .Select(v => v.Id)
                .ToListAsync(stoppingToken);

            foreach (var id in pendingIds)
                _queue.Enqueue(id);

            if (pendingIds.Count > 0)
                _logger.LogInformation("Re-enqueued {Count} pending verification(s) on startup", pendingIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-enqueue pending verifications on startup");
        }
    }
}
