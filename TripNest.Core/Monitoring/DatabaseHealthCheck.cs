using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TripNest.Core.Context;

namespace TripNest.Core.Monitoring;

/// <summary>
/// Readiness check that confirms the Postgres database is actually reachable (not just that the
/// process is up). Critical dependency — a failure marks <c>/health/ready</c> Unhealthy (503) so an
/// orchestrator/load balancer stops routing traffic to an instance that can't serve requests.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // Bound the probe so a hung connection doesn't hang the health endpoint.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var canConnect = await _db.Database.CanConnectAsync(cts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("Database reachable")
                : new HealthCheckResult(context.Registration.FailureStatus, "Database unreachable");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Database probe failed", ex);
        }
    }
}
