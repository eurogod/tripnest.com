using TripNest.Core.DTOs.Agreements;
using TripNest.Core.Exceptions;
using TripNest.Core.DTOs.Shared;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class AgreementService : IAgreementService
{
    private readonly IAgreementRepository _agreementRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<AgreementService> _logger;

    public AgreementService(
        IAgreementRepository agreementRepository,
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IFileStorage fileStorage,
        ILogger<AgreementService> logger)
    {
        _agreementRepository = agreementRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    /// <summary>SHA-256 (hex, lowercase) of the terms text — what both signatures bind to.</summary>
    private static string HashTerms(string termsContent) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(termsContent))).ToLowerInvariant();

    public async Task<AgreementResponse> CreateAgreementAsync(string bookingId, string userId)
    {
        try
        {
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
            if (booking == null)
                throw new NotFoundException("Booking");

            // Confirmed (the normal case) or Completed (an agreement generated at escrow release,
            // for records + both-party signing).
            if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed))
                throw new ValidationException("Agreement can only be created for confirmed bookings");

            var existing = await _agreementRepository.GetByBookingIdAsync(bookingId);
            if (existing != null)
                throw new ConflictException("An agreement already exists for this booking");

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
                Status = AgreementStatus.Draft,
                // The agreement covers the stay; past checkout it is spent paper.
                ExpiryDate = booking.CheckOutDate
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

    public async Task<PagedResult<AgreementResponse>> GetUserAgreementsAsync(string userId, int page, int pageSize)
    {
        // Narrow to the user's agreements in the database (tenant on the booking, or the property's
        // landlord) instead of loading every agreement and filtering in memory.
        var agreements = (await _agreementRepository.FindAsync(
                a => a.Booking!.TenantId == userId || a.Booking!.Property!.UserId == userId))
            .OrderByDescending(a => a.CreatedAt)
            .ToList();

        // Page before the per-agreement booking fallback below so at most one page's worth of
        // detail lookups run per request.
        var paged = Paging.Page(agreements, page, pageSize);
        var result = new List<AgreementResponse>();

        foreach (var agreement in paged.Items)
        {
            var booking = agreement.Booking
                ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

            if (booking == null)
                continue;

            var landlordId = booking.Property?.UserId;

            if (booking.TenantId == userId || landlordId == userId)
                result.Add(MapToResponse(agreement));
        }

        return new PagedResult<AgreementResponse>
        {
            Items = result,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
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

        return MapToResponse(await WithLazyExpiryAsync(agreement));
    }

    public async Task SignAgreementAsync(string agreementId, string userId)
    {
        try
        {
            var agreement = await _agreementRepository.GetByIdAsync(agreementId);
            if (agreement == null)
                throw new NotFoundException("Agreement");

            if (agreement.Status != AgreementStatus.Draft && agreement.Status != AgreementStatus.Pending)
                throw new ValidationException("Agreement cannot be signed in its current status");

            var booking = agreement.Booking
                ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId);

            if (booking == null)
                throw new NotFoundException("Booking associated with agreement");

            var landlordId = booking.Property?.UserId;

            // Tamper evidence: the first signature captures the hash of the terms; the second
            // refuses to bind if the text no longer hashes to the same value — nobody can sign a
            // document different from what the first party signed.
            var termsHash = HashTerms(agreement.TermsContent);
            if (agreement.TermsHash is null)
                agreement.TermsHash = termsHash;
            else if (agreement.TermsHash != termsHash)
                throw new ConflictException(
                    "The agreement terms changed after the first signature — signing is blocked. Contact support.");

            var signature = $"SIGNED:{userId}:{DateTime.UtcNow:O}";

            // Snapshot the signer's current profile signature image (may be null — the click
            // record above is the actual signature; the image is presentation on the PDF).
            var signerImagePath = (await _userRepository.GetByIdAsync(userId))?.SignatureImagePath;

            if (booking.TenantId == userId)
            {
                agreement.TenantSignature = signature;
                agreement.TenantSignatureImagePath = signerImagePath;
            }
            else if (landlordId == userId)
            {
                agreement.LandlordSignature = signature;
                agreement.LandlordSignatureImagePath = signerImagePath;
            }
            else
            {
                throw new ForbiddenException("You are not authorised to sign this agreement");
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
            throw new NotFoundException("Agreement");

        var booking = agreement.Booking
            ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId)
            // Authorisation is derived from the booking's parties — without it we cannot prove the
            // caller's right to the document, so deny rather than serve it to anyone.
            ?? throw new NotFoundException("Booking associated with agreement");

        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new ForbiddenException("You are not authorised to download this agreement");

        var bytes = Pdf.AgreementPdf.Render(agreement, booking,
            await TryReadImageAsync(agreement.TenantSignatureImagePath),
            await TryReadImageAsync(agreement.LandlordSignatureImagePath));
        var filename = $"agreement-{agreementId}.pdf";

        _logger.LogInformation("Agreement {AgreementId} downloaded by user {UserId}", agreementId, userId);

        return (bytes, filename);
    }

    /// <summary>Best-effort load of a snapshotted signature image for the PDF; a missing or
    /// unreadable file falls back to the text signature block rather than failing the download.</summary>
    private async Task<byte[]?> TryReadImageAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            await using var stream = await _fileStorage.OpenReadAsync(path);
            if (stream is null)
                return null;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load signature image {Path} for agreement PDF", path);
            return null;
        }
    }

    /// <summary>Either party may terminate a signed agreement (a record-keeping action —
    /// refunds/cancellation money flows live in the booking/escrow modules).</summary>
    public async Task<AgreementResponse> TerminateAgreementAsync(string agreementId, string userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("A termination reason is required");

        var agreement = await _agreementRepository.GetByIdAsync(agreementId)
            ?? throw new NotFoundException("Agreement");
        var booking = agreement.Booking
            ?? await _bookingRepository.GetByIdWithDetailsAsync(agreement.BookingId)
            ?? throw new NotFoundException("Booking associated with agreement");

        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new ForbiddenException("You are not a party to this agreement");
        if (agreement.Status != AgreementStatus.Signed)
            throw new ValidationException("Only a signed agreement can be terminated");

        agreement.Status = AgreementStatus.Terminated;
        agreement.TermsContent += $"\n\nTERMINATED by {userId} on {DateTime.UtcNow:yyyy-MM-dd}: {reason.Trim()}";
        await _agreementRepository.UpdateAsync(agreement);
        await _agreementRepository.SaveChangesAsync();

        _logger.LogInformation("Agreement {AgreementId} terminated by {UserId}", agreementId, userId);
        return MapToResponse(agreement);
    }

    /// <summary>Lazily retires a signed agreement whose stay ended — no worker needed; the next
    /// read persists the transition.</summary>
    private async Task<Agreement> WithLazyExpiryAsync(Agreement agreement)
    {
        if (agreement.Status == AgreementStatus.Signed &&
            agreement.ExpiryDate is { } expiry && expiry < DateTime.UtcNow)
        {
            agreement.Status = AgreementStatus.Expired;
            await _agreementRepository.UpdateAsync(agreement);
            await _agreementRepository.SaveChangesAsync();
        }
        return agreement;
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
            TermsHash = a.TermsHash,
            ExpiryDate = a.ExpiryDate
        };
}
