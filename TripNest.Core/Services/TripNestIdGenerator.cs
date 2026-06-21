namespace TripNest.Core.Services;

/// <summary>
/// Mints the public TripNest identity badge in the form <c>TN-GH-{issue-year}-{6-digit serial}</c>.
/// The serial is a global, monotonically-assigned number (the year is the year of issue, NOT a
/// per-year counter), so the full value is always unique. Both the real verification flow and the
/// dev seeder mint IDs through here so generated values share one format and never overlap.
/// </summary>
public static class TripNestIdGenerator
{
    public static string Format(int serial) => $"TN-GH-{DateTime.UtcNow.Year}-{serial:D6}";
}
