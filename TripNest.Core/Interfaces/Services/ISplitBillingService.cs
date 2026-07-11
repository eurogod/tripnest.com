using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Models;

namespace TripNest.Core.Interfaces.Services;

public interface ISplitBillingService
{
    /// <summary>
    /// Creates the shares for a just-created group booking (called from booking creation, saved in
    /// the same transaction): the total split equally, booker absorbs rounding, every co-traveller
    /// must be a registered user. Returns the share rows (unsaved — the caller commits).
    /// </summary>
    Task<List<BookingShare>> BuildSharesAsync(Booking booking, string bookerId, List<string> coTravellerEmails);

    /// <summary>Shares of a booking, visible to its members and the property's landlord.</summary>
    Task<List<BookingShareResponse>> GetSharesAsync(string bookingId, string userId);

    /// <summary>Starts the calling participant's own checkout for their share.</summary>
    Task<BookingShareResponse> InitiateSharePaymentAsync(string shareId, string userId);

    /// <summary>Actively confirms the caller's share with the provider (fallback for a lost webhook).</summary>
    Task<BookingShareResponse> VerifySharePaymentAsync(string shareId, string userId);

    /// <summary>Applies a signature-verified provider webhook for a share charge. When the last
    /// share lands, holds the booking's escrow and confirms the booking.</summary>
    Task ApplySharePaymentAsync(string shareId, string reference, decimal paidAmount);

    /// <summary>Cancels group bookings whose payment window elapsed and refunds paid shares (worker path).</summary>
    Task ExpireOverdueAsync(CancellationToken cancellationToken = default);
}
