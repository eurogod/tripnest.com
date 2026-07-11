namespace TripNest.Core.Enums;

public enum RentInvoiceStatus
{
    /// <summary>Scheduled for a future period; not yet payable-reminded.</summary>
    Upcoming,
    /// <summary>Inside the reminder window (or past period start) — the tenant should pay now.</summary>
    Due,
    Paid,
    /// <summary>Past the due date without payment.</summary>
    Overdue,
    /// <summary>Voided — the underlying booking was cancelled.</summary>
    Cancelled
}
