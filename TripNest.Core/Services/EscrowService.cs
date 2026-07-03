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
    private readonly IUserRepository _userRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly ILogger<EscrowService> _logger;

    public EscrowService(
        IEscrowRepository escrowRepository,
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IPaymentGateway paymentGateway,
        ILogger<EscrowService> logger)
    {
        _escrowRepository = escrowRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _paymentGateway = paymentGateway;
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

        // Start a Paystack checkout for the booking's tenant. The tenant always exists (the booking
        // references them and we authorized against TenantId above) — fail loudly rather than send
        // the receipt to a bogus address.
        var tenant = await _userRepository.GetByIdAsync(booking.TenantId)
            ?? throw new InvalidOperationException("Booking tenant not found");
        var payment = await _paymentGateway.InitiatePaymentAsync(
            escrow.Amount, "GHS", tenant.Email, bookingId);
        escrow.PaymentReference = payment.Reference;

        await _escrowRepository.AddAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow initiated for booking {BookingId}, escrow {EscrowId}, amount {Amount}, ref {Reference}",
            bookingId, escrow.Id, escrow.Amount, payment.Reference);

        return MapToResponse(escrow, payment.CheckoutUrl);
    }

    public async Task VerifyAndHoldPaymentAsync(string bookingId, string reference, decimal paidAmount)
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

        // The webhook is authenticated (HMAC signature), but the amount must still match what the
        // booking is owed — never trust a "success" event to mean the correct amount was paid.
        // Allow a 1-pesewa tolerance for rounding; reject genuine under/over-payment.
        if (Math.Abs(paidAmount - escrow.Amount) > 0.01m)
        {
            _logger.LogWarning(
                "Escrow {EscrowId} payment amount mismatch for booking {BookingId}: expected {Expected}, paid {Paid} (ref {Reference})",
                escrow.Id, bookingId, escrow.Amount, paidAmount, reference);
            throw new InvalidOperationException(
                $"Paid amount ({paidAmount:0.00}) does not match the amount due ({escrow.Amount:0.00})");
        }

        escrow.Status = EscrowStatus.HeldInEscrow;
        escrow.PaymentReference = reference;
        escrow.HeldAt = DateTime.UtcNow;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} held for booking {BookingId} (reference: {Reference})",
            escrow.Id, bookingId, reference);
    }

    public async Task<EscrowResponse> VerifyPaymentByBookingAsync(string bookingId, string userId)
    {
        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId)
            ?? throw new InvalidOperationException($"No escrow found for booking '{bookingId}'");

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException($"Booking '{bookingId}' not found");

        // Only the tenant who owns the booking may verify its payment.
        if (booking.TenantId != userId)
            throw new InvalidOperationException("Only the booking's tenant can verify its payment");

        // Already held — nothing to do (idempotent with the webhook path).
        if (escrow.Status == EscrowStatus.HeldInEscrow)
            return MapToResponse(escrow);

        if (string.IsNullOrEmpty(escrow.PaymentReference))
            throw new InvalidOperationException("No payment has been started for this booking yet.");

        // Ask the provider directly whether the charge succeeded, instead of waiting for the webhook.
        var result = await _paymentGateway.VerifyPaymentAsync(escrow.PaymentReference);
        if (!result.Success)
            throw new InvalidOperationException("Payment has not been completed for this booking yet.");

        // Reuse the exact same guarded transition the webhook uses: it re-checks the amount and is
        // idempotent, so a verify racing the webhook can't double-hold or hold the wrong amount.
        await VerifyAndHoldPaymentAsync(bookingId, escrow.PaymentReference, result.Amount);

        var updated = await _escrowRepository.GetByBookingIdAsync(bookingId);
        return MapToResponse(updated!);
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

    public async Task<EscrowResponse?> GetEscrowByBookingAsync(string bookingId, string userId)
    {
        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
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

        // Parity with the auto-release job: a released stay is a completed stay.
        // NOTE: actual disbursement to the landlord happens via Paystack Transfers, which requires a
        // verified transfer recipient per host. Until that is wired, "Released" records intent only.
        booking.Status = BookingStatus.Completed;

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

        // Issue the refund through the provider before recording it locally. Only mark the escrow
        // refunded if the provider actually accepted it — otherwise the status would lie about money
        // that never moved. (The provider call is idempotent on the same transaction reference.)
        if (!string.IsNullOrEmpty(escrow.PaymentReference))
        {
            var refunded = await _paymentGateway.RefundAsync(escrow.PaymentReference, escrow.Amount);
            if (!refunded)
                throw new InvalidOperationException("Refund could not be processed by the payment provider. Please retry.");
        }

        escrow.Status = EscrowStatus.Refunded;
        escrow.ReleaseReason = reason;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} refunded: {Reason}", escrowId, reason);
    }

    private static EscrowResponse MapToResponse(Escrow e, string? checkoutUrl = null) => new EscrowResponse
    {
        EscrowId = e.Id,
        BookingId = e.BookingId,
        Amount = e.Amount,
        Status = e.Status,
        CreatedAt = e.CreatedAt,
        HeldAt = e.HeldAt,
        ReleasedAt = e.ReleasedAt,
        ReleaseReason = e.ReleaseReason,
        PaymentReference = e.PaymentReference,
        CheckoutUrl = checkoutUrl,
        // A Released escrow records payout intent; funds don't actually reach the host until
        // Paystack Transfers are wired (see ReleaseEscrowAsync). Flag it so the UI stays honest.
        DisbursementPending = e.Status == EscrowStatus.Released
    };
}
