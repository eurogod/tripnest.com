using TripNest.Core.DTOs.Rent;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

public interface IRentService
{
    /// <summary>Prefix marking a provider metadata bookingId as a rent-invoice charge.</summary>
    const string ReferencePrefix = "rent:";
    /// <summary>Rent periods are fixed 30-day blocks, matching the pro-rata convention.</summary>
    const int RentPeriodDays = 30;

    /// <summary>
    /// Builds the monthly invoice schedule for a long-term booking's periods AFTER the first
    /// (the first is the upfront escrow payment). Rows are added unsaved — the caller commits
    /// them atomically with the booking.
    /// </summary>
    Task<List<RentInvoice>> BuildScheduleAsync(Booking booking, string landlordId, decimal monthlyRent);

    /// <summary>The caller's rent invoices across bookings (as tenant), soonest due first.</summary>
    Task<PagedResult<RentInvoiceResponse>> GetMyInvoicesAsync(string tenantId, int page, int pageSize);

    /// <summary>A booking's rent schedule — its tenant or the property's landlord only.</summary>
    Task<List<RentInvoiceResponse>> GetForBookingAsync(string bookingId, string userId);

    /// <summary>Starts the tenant's checkout for one invoice (metadata "rent:{id}").</summary>
    Task<RentInvoiceResponse> InitiatePaymentAsync(string invoiceId, string userId);

    /// <summary>Actively confirms an invoice with the provider (fallback for a lost webhook).</summary>
    Task<RentInvoiceResponse> VerifyPaymentAsync(string invoiceId, string userId);

    /// <summary>Applies a signature-verified provider webhook for a rent charge: marks the invoice
    /// paid and creates the landlord's payout net of the platform fee.</summary>
    Task ApplyRentPaymentAsync(string invoiceId, string reference, decimal paidAmount);

    /// <summary>Voids the outstanding (unpaid) invoices of a cancelled booking.</summary>
    Task CancelOutstandingAsync(string bookingId);

    /// <summary>Daily sweep: move invoices into Due (reminder window) and Overdue, notifying parties.</summary>
    Task ProcessDueAndOverdueAsync(CancellationToken cancellationToken = default);
}
