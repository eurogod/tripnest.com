namespace TripNest.Core.Enums;

/// <summary>
/// Refund tier a landlord chooses for a listing:
/// - Flexible: full refund if cancelled 24h+ before check-in, else 0%.
/// - Moderate: full refund 5+ days before, 50% 1-5 days before, 0% within 24h.
/// - Strict: 50% refund 7+ days before, 0% within 7 days.
/// </summary>
public enum CancellationPolicy
{
    Flexible,
    Moderate,
    Strict
}
