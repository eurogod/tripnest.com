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

    public async Task<EscrowResponse> InitiatePaymentAsync(string bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
        if (booking == null)
            throw new InvalidOperationException($"Booking '{bookingId}' not found");

        // Only the tenant who owns the booking may initiate its payment.
        if (booking.TenantId != userId)
            throw new InvalidOperationException("Only the booking's tenant can initiate payment");

        // Idempotency: a booking has at most one escrow. If one already exists,
        // return it rather than creating a duplicate.
        var existing = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (existing != null)
            return MapToResponse(existing);

        // The amount is derived from the booking server-side — never trusted from the client.
        var escrow = new Escrow
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount,
            Status = EscrowStatus.Pending
        };

        await _escrowRepository.AddAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow initiated for booking {BookingId}, escrow {EscrowId}, amount {Amount}",
            bookingId, escrow.Id, escrow.Amount);

        return MapToResponse(escrow);
    }

    public async Task VerifyAndHoldPaymentAsync(string bookingId, string reference)
    {
        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (escrow == null)
            throw new InvalidOperationException($"No escrow found for booking '{bookingId}'");

        // Idempotency: payment providers retry webhooks. If this escrow is already
        // held with the same reference, treat the call as a successful no-op.
        if (escrow.Status == EscrowStatus.HeldInEscrow && escrow.PaymentReference == reference)
        {
            _logger.LogInformation("Escrow {EscrowId} already held for reference {Reference} — ignoring duplicate webhook",
                escrow.Id, reference);
            return;
        }

        // Funds can only move into escrow from the Pending state.
        if (escrow.Status != EscrowStatus.Pending)
            throw new InvalidOperationException($"Escrow cannot be held from status '{escrow.Status}'");

        escrow.Status = EscrowStatus.HeldInEscrow;
        escrow.PaymentReference = reference;
        escrow.HeldAt = DateTime.UtcNow;

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

        if (escrow.Status != EscrowStatus.Disputed)
            throw new InvalidOperationException($"Only a disputed escrow can be resolved (current status: '{escrow.Status}')");

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

        // Funds that have already been released or refunded cannot be refunded again.
        if (escrow.Status is not (EscrowStatus.HeldInEscrow or EscrowStatus.Disputed))
            throw new InvalidOperationException($"Escrow cannot be refunded from status '{escrow.Status}'");

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
        HeldAt = e.HeldAt,
        ReleasedAt = e.ReleasedAt,
        ReleaseReason = e.ReleaseReason
    };
}
