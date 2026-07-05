using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace TripNest.Core.Monitoring;

/// <summary>
/// Serilog enricher that stamps each log event with the current distributed-trace ids (W3C
/// <c>TraceId</c>/<c>SpanId</c> from <see cref="Activity.Current"/>). This is what lets a single
/// request be correlated across log lines — and, in Application Insights, joins logs to their trace.
/// </summary>
public sealed class ActivityEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null)
            return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("TraceId", activity.TraceId.ToString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SpanId", activity.SpanId.ToString()));
    }
}
