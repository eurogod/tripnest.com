using System.Globalization;

namespace TripNest.Core.Services;

/// <summary>
/// Minimal RFC 5545 reader for the one thing calendar import needs: each VEVENT's busy range.
/// Handles folded lines, DATE and DATE-TIME (with or without Z/TZID) values, and events with a
/// missing DTEND (treated as a single day). Anything unparseable is skipped, not fatal — a feed
/// with one odd event should still sync the rest.
/// </summary>
public static class IcalParser
{
    private static readonly string[] DateFormats =
    {
        "yyyyMMdd", "yyyyMMdd'T'HHmmss", "yyyyMMdd'T'HHmmss'Z'",
    };

    public static IReadOnlyList<(DateTime Start, DateTime End)> ParseBusyRanges(string ics)
    {
        var ranges = new List<(DateTime, DateTime)>();
        if (string.IsNullOrWhiteSpace(ics))
            return ranges;

        // Unfold: a line starting with space/tab continues the previous one (RFC 5545 §3.1).
        var raw = ics.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();
        foreach (var line in raw)
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && lines.Count > 0)
                lines[^1] += line[1..];
            else
                lines.Add(line);
        }

        DateTime? start = null, end = null;
        var inEvent = false;
        foreach (var line in lines)
        {
            if (line.StartsWith("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                inEvent = true;
                start = end = null;
            }
            else if (line.StartsWith("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (inEvent && start.HasValue)
                {
                    // DTEND is exclusive; a missing/degenerate one means a one-day event.
                    var rangeEnd = end.HasValue && end.Value > start.Value ? end.Value : start.Value.AddDays(1);
                    ranges.Add((start.Value, rangeEnd));
                }
                inEvent = false;
            }
            else if (inEvent && line.StartsWith("DTSTART", StringComparison.OrdinalIgnoreCase))
            {
                start = ParseDate(line);
            }
            else if (inEvent && line.StartsWith("DTEND", StringComparison.OrdinalIgnoreCase))
            {
                end = ParseDate(line);
            }
        }

        return ranges;
    }

    private static DateTime? ParseDate(string line)
    {
        // "DTSTART;VALUE=DATE:20260801" / "DTSTART:20260801T140000Z" / "DTSTART;TZID=...:20260801T140000"
        var colon = line.IndexOf(':');
        if (colon < 0 || colon == line.Length - 1)
            return null;

        var value = line[(colon + 1)..].Trim();
        if (DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return parsed.Date; // busy ranges are day-granular for availability purposes

        return null;
    }
}
