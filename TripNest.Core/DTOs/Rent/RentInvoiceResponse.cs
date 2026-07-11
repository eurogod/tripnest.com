using TripNest.Core.Enums;

namespace TripNest.Core.DTOs.Rent;

public class RentInvoiceResponse
{
    public required string InvoiceId { get; set; }
    public required string BookingId { get; set; }
    public required string PropertyId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }
    public RentInvoiceStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Set only on the pay response — the tenant's checkout link.</summary>
    public string? CheckoutUrl { get; set; }
}
