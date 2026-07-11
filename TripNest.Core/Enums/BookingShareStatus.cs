namespace TripNest.Core.Enums;

public enum BookingShareStatus
{
    Pending,
    Paid,
    /// <summary>The share was paid but returned — the group missed the payment window or the
    /// booking lost the double-booking race.</summary>
    Refunded
}
