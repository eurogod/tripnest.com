using TripNest.Core.Enums;

namespace TripNest.Core.Models;

/// <summary>
/// One month's rent for a long-term booking. Long stays don't charge the whole stay upfront:
/// the booking's escrow covers only the first period, and each later period becomes an invoice
/// the tenant pays month by month (own checkout, metadata "rent:{id}"). A paid invoice creates
/// the landlord's payout (net of the platform fee) immediately — no escrow hold, since the
/// tenant is already living in the property.
/// </summary>
public class RentInvoice
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string BookingId { get; set; }
    public Booking? Booking { get; set; }
    /// <summary>Denormalized from the booking for cheap "my invoices" queries.</summary>
    public required string TenantId { get; set; }
    public required string LandlordId { get; set; }

    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    /// <summary>Rent for the period (the final partial month is pro-rated).</summary>
    public decimal Amount { get; set; }
    /// <summary>When payment is expected — the period's first day.</summary>
    public DateTime DueDate { get; set; }

    public RentInvoiceStatus Status { get; set; } = RentInvoiceStatus.Upcoming;
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
