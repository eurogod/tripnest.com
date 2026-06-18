using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Enums;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class EscrowService : IEscrowService
{
    private readonly IEscrowRepository _escrowRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<EscrowService> _logger;

    public EscrowService(
        IEscrowRepository escrowRepository,
        IBookingRepository bookingRepository,
        ILogger<EscrowService> logger)
    {
        _escrowRepository = escrowRepository;
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task<EscrowResponse> InitiatePaymentAsync(string bookingId, decimal amount)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new InvalidOperationException($"Booking '{bookingId}' not found");

        var escrow = new Escrow
        {
            BookingId = bookingId,
            Amount = amount,
            Status = EscrowStatus.Pending
        };

        await _escrowRepository.AddAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow initiated for booking {BookingId}, escrow {EscrowId}", bookingId, escrow.Id);

        return MapToResponse(escrow);
    }

    public async Task VerifyAndHoldPaymentAsync(string bookingId, string reference)
    {
        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (escrow == null)
            throw new InvalidOperationException($"No escrow found for booking '{bookingId}'");

        escrow.Status = EscrowStatus.HeldInEscrow;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} held for booking {BookingId} (reference: {Reference})",
            escrow.Id, bookingId, reference);
    }

    public async Task<EscrowResponse?> GetEscrowAsync(string escrowId, string userId)
    {
        var escrow = await _escrowRepository.GetByIdAsync(escrowId);
        if (escrow == null)
            return null;

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(escrow.BookingId);
        if (booking == null)
            return null;

        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            return null;

        return MapToResponse(escrow);
    }

    public async Task ReleaseEscrowAsync(string escrowId, string userId)
    {
        var escrow = await _escrowRepository.GetByIdAsync(escrowId);
        if (escrow == null)
            throw new InvalidOperationException($"Escrow '{escrowId}' not found");

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(escrow.BookingId);
        if (booking == null)
            throw new InvalidOperationException($"Booking '{escrow.BookingId}' not found");

        var landlordId = booking.Property?.UserId;
        if (landlordId != userId)
            throw new InvalidOperationException("Only the landlord can release escrow funds");

        if (escrow.Status != EscrowStatus.HeldInEscrow)
            throw new InvalidOperationException($"Escrow cannot be released from status '{escrow.Status}'");

        escrow.Status = EscrowStatus.Released;
        escrow.ReleasedAt = DateTime.UtcNow;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} released by landlord {UserId}", escrowId, userId);
    }

    public async Task RaiseDisputeAsync(string escrowId, string userId, string reason)
    {
        var escrow = await _escrowRepository.GetByIdAsync(escrowId);
        if (escrow == null)
            throw new InvalidOperationException($"Escrow '{escrowId}' not found");

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(escrow.BookingId);
        if (booking == null)
            throw new InvalidOperationException($"Booking '{escrow.BookingId}' not found");

        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new InvalidOperationException("Only the tenant or landlord can raise a dispute");

        escrow.Status = EscrowStatus.Disputed;
        escrow.ReleaseReason = reason;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Dispute raised on escrow {EscrowId} by user {UserId}: {Reason}",
            escrowId, userId, reason);
    }

    public async Task ResolveDisputeAsync(string escrowId, bool approved)
    {
        var escrow = await _escrowRepository.GetByIdAsync(escrowId);
        if (escrow == null)
            throw new InvalidOperationException($"Escrow '{escrowId}' not found");

        escrow.Status = approved ? EscrowStatus.Released : EscrowStatus.Refunded;
        if (approved)
            escrow.ReleasedAt = DateTime.UtcNow;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Dispute resolved for escrow {EscrowId}: approved={Approved}, new status={Status}",
            escrowId, approved, escrow.Status);
    }

    public async Task RefundEscrowAsync(string escrowId, string reason)
    {
        var escrow = await _escrowRepository.GetByIdAsync(escrowId);
        if (escrow == null)
            throw new InvalidOperationException($"Escrow '{escrowId}' not found");

        escrow.Status = EscrowStatus.Refunded;
        escrow.ReleaseReason = reason;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} refunded: {Reason}", escrowId, reason);
    }

    private static EscrowResponse MapToResponse(Escrow e) => new EscrowResponse
    {
        EscrowId = e.Id,
        BookingId = e.BookingId,
        Amount = e.Amount,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
        ReleasedAt = e.ReleasedAt,
        ReleaseReason = e.ReleaseReason
    };
}
