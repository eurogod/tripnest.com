using System.ComponentModel.DataAnnotations;

namespace TripNest.Core.Options;

/// <summary>
/// Platform-level business settings, bound from the "Platform" section and validated at startup
/// (<c>ValidateOnStart</c> in Program.cs) — a typo'd key or out-of-range value fails the boot,
/// not a payout. Defaults here match appsettings.json; the options class is the single source
/// every consumer reads, so the fee used in payouts, statements and earnings breakdowns can
/// never silently diverge.
/// </summary>
public class PlatformOptions
{
    public const string SectionName = "Platform";

    /// <summary>Management fee the platform keeps from each booking's gross revenue, as a percent.</summary>
    [Range(0, 100)]
    public decimal ManagementFeePercent { get; set; } = 10m;

    /// <summary>ISO currency for all money movement (charges, refunds, transfers).</summary>
    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "GHS";

    /// <summary>
    /// Platform-wide cancellation grace period: a booking cancelled within this many hours of
    /// being created refunds 100% regardless of the listing's policy. 0 disables the guarantee.
    /// </summary>
    [Range(0, 24 * 7)]
    public int CancellationGraceHours { get; set; } = 48;

    /// <summary>The grace guarantee only applies while check-in is at least this many days away
    /// (prevents booking-then-cancelling on the eve of a stay to dodge a strict policy).</summary>
    [Range(0, 30)]
    public int CancellationGraceMinDaysBeforeCheckIn { get; set; } = 2;
}

/// <summary>Escrow lifecycle settings, bound from the "Escrow" section and validated at startup.</summary>
public class EscrowOptions
{
    public const string SectionName = "Escrow";

    /// <summary>Hours after checkout before held funds auto-release to the host (the dispute window).</summary>
    [Range(1, 24 * 30)]
    public int GracePeriodHours { get; set; } = 24;
}
