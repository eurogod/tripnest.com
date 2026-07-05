using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TripNest.Core.Monitoring;

/// <summary>
/// Readiness check for an external HTTP dependency (the TripNest.Id authority and the face-match
/// sidecar). Any HTTP response means the service is reachable; a connection failure/timeout reports
/// the configured failure status. These are registered with <see cref="HealthStatus.Degraded"/> —
/// they are only needed for Ghana Card verification, so the API stays "ready" (200) when they're
/// down, but the degraded state is still surfaced in the report for monitoring.
/// </summary>
public sealed class HttpDependencyHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _url;

    public HttpDependencyHealthCheck(IHttpClientFactory httpClientFactory, string url)
    {
        _httpClientFactory = httpClientFactory;
        _url = url;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_url))
            return new HealthCheckResult(context.Registration.FailureStatus, "No URL configured");

        try
        {
            var client = _httpClientFactory.CreateClient("health");
            client.Timeout = TimeSpan.FromSeconds(3);

            // We don't assume a specific health route — any HTTP response proves reachability.
            using var response = await client.GetAsync(_url, cancellationToken);
            return HealthCheckResult.Healthy($"Reachable (HTTP {(int)response.StatusCode})");
        }
        catch (Exception ex)
        {
            // Include the message but NOT the exception object: readiness is polled frequently and a
            // routinely-down optional sidecar shouldn't spew a stack trace on every probe.
            return new HealthCheckResult(context.Registration.FailureStatus, $"Unreachable at {_url}: {ex.Message}");
        }
    }
}
