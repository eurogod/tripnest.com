using TripNest.Core.DTOs.Agreements;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AgreementService : IAgreementService
{
    private readonly IAgreementRepository _agreementRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<AgreementService> _logger;

    public AgreementService(
        IAgreementRepository agreementRepository,
        IBookingRepository bookingRepository,
        ILogger<AgreementService> logger)
    {
        _agreementRepository = agreementRepository;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task<AgreementResponse> CreateAgreementAsync(string bookingId, string userId)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
            if (booking == null)
                throw new InvalidOperationException("Booking not found");

            if (booking.Status != BookingStatus.Confirmed)
                throw new InvalidOperationException("Agreement can only be created for confirmed bookings");

            var existing = await _agreementRepository.GetByBookingIdAsync(bookingId);
            if (existing != null)
                throw new InvalidOperationException("An agreement already exists for this booking");

            var propertyTitle = booking.Property?.Title ?? bookingId;
            var propertyLocation = booking.Property?.Location ?? string.Empty;
            var tenantName = booking.Tenant?.FullName ?? booking.TenantId;

            var termsContent = $"""
                RENTAL AGREEMENT
                ================
                Property  : {propertyTitle}
                Location  : {propertyLocation}
                Tenant    : {tenantName}
                Check-In  : {booking.CheckInDate:yyyy-MM-dd}
                Check-Out : {booking.CheckOutDate:yyyy-MM-dd}
                Amount    : {booking.TotalAmount:C}
                Booking ID: {booking.Id}

                This agreement is legally binding upon signature by both the tenant and landlord.
                Both parties agree to comply with all terms and conditions of TripNest and applicable local laws.
                """;

            var agreement = new Agreement
            {
                BookingId = bookingId,
                TermsContent = termsContent,
                Status = AgreementStatus.Draft
            };

            await _agreementRepository.AddAsync(agreement);
            await _agreementRepository.SaveChangesAsync();

            _logger.LogInformation("Agreement created: {AgreementId} for booking {BookingId}", agreement.Id, bookingId);

            return MapToResponse(agreement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agreement for booking {BookingId}", bookingId);
            throw;
        }
    }

    public async Task<List<AgreementResponse>> GetUserAgreementsAsync(string userId)
    {
        var allAgreements = await _agreementRepository.GetAllAsync();
        var result = new List<AgreementResponse>();

        foreach (var agreement in allAgreements)
        {
            var booking = agreement.Booking
                ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

            if (booking == null)
                continue;

            var landlordId = booking.Property?.UserId;

            if (booking.TenantId == userId || landlordId == userId)
                result.Add(MapToResponse(agreement));
        }

        return result;
    }

    public async Task<AgreementResponse?> GetAgreementAsync(string agreementId, string userId)
    {
        var agreement = await _agreementRepository.GetByIdAsync(agreementId);
        if (agreement == null)
            return null;

        var booking = agreement.Booking
            ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

        if (booking == null)
            return null;

        var landlordId = booking.Property?.UserId;

        if (booking.TenantId != userId && landlordId != userId)
            return null;

        return MapToResponse(agreement);
    }

    public async Task SignAgreementAsync(string agreementId, string userId)
    {
        try
        {
            var agreement = await _agreementRepository.GetByIdAsync(agreementId);
            if (agreement == null)
                throw new InvalidOperationException("Agreement not found");

            if (agreement.Status != AgreementStatus.Draft && agreement.Status != AgreementStatus.Pending)
                throw new InvalidOperationException("Agreement cannot be signed in its current status");

            var booking = agreement.Booking
                ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

            if (booking == null)
                throw new InvalidOperationException("Booking associated with agreement not found");

            var landlordId = booking.Property?.UserId;

            var signature = $"SIGNED:{userId}:{DateTime.UtcNow:O}";

            if (booking.TenantId == userId)
            {
                agreement.TenantSignature = signature;
            }
            else if (landlordId == userId)
            {
                agreement.LandlordSignature = signature;
            }
            else
            {
                throw new UnauthorizedAccessException("User is not authorised to sign this agreement");
            }

            if (!string.IsNullOrEmpty(agreement.TenantSignature) &&
                !string.IsNullOrEmpty(agreement.LandlordSignature))
            {
                agreement.Status = AgreementStatus.Signed;
                agreement.SignedAt = DateTime.UtcNow;
            }
            else
            {
                agreement.Status = AgreementStatus.Pending;
            }

            await _agreementRepository.UpdateAsync(agreement);
            await _agreementRepository.SaveChangesAsync();

            _logger.LogInformation("Agreement {AgreementId} signed by user {UserId}", agreementId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing agreement {AgreementId} for user {UserId}", agreementId, userId);
            throw;
        }
    }

    public async Task<(byte[], string)> DownloadAgreementPdfAsync(string agreementId, string userId)
    {
        var agreement = await _agreementRepository.GetByIdAsync(agreementId);
        if (agreement == null)
            throw new InvalidOperationException("Agreement not found");

        var booking = agreement.Booking
            ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

        if (booking != null)
        {
            var landlordId = booking.Property?.UserId;
            if (booking.TenantId != userId && landlordId != userId)
                throw new UnauthorizedAccessException("User is not authorised to download this agreement");
        }

        var bytes = Pdf.AgreementPdf.Render(agreement, booking);
        var filename = $"agreement-{agreementId}.pdf";

        _logger.LogInformation("Agreement {AgreementId} downloaded by user {UserId}", agreementId, userId);

        return (bytes, filename);
    }

    private static AgreementResponse MapToResponse(Agreement a) =>
        new AgreementResponse
        {
            AgreementId = a.Id,
            BookingId = a.BookingId,
            TermsContent = a.TermsContent,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            SignedAt = a.SignedAt,
            TenantSignature = a.TenantSignature,
            LandlordSignature = a.LandlordSignature,
            ExpiryDate = a.ExpiryDate
        };
}
