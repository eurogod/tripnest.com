using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TripNest.Core.DTOs.Escrow;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Options;

namespace TripNest.Core.Services;

public class EscrowService : IEscrowService
{
    private readonly IEscrowRepository _escrowRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IPayoutService _payoutService;
    private readonly IRepository<EscrowEvent> _escrowEventRepository;
    private readonly PlatformOptions _platform;
    private readonly ILogger<EscrowService> _logger;

    public EscrowService(
        IEscrowRepository escrowRepository,
        IBookingRepository bookingRepository,
        IUserRepository userRepository,
        IPaymentGateway paymentGateway,
        IPayoutService payoutService,
        IRepository<EscrowEvent> escrowEventRepository,
        IOptions<PlatformOptions> platformOptions,
        ILogger<EscrowService> logger)
    {
        _escrowRepository = escrowRepository;
        _bookingRepository = bookingRepository;
        _userRepository = userRepository;
        _paymentGateway = paymentGateway;
        _payoutService = payoutService;
        _escrowEventRepository = escrowEventRepository;
        _platform = platformOptions.Value;
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

        // Idempotency: a booking has at most one escrow. CreateBookingAsync already creates a
        // Pending escrow with the booking, so the common case is finding one here. Escrows whose
        // funds have already moved (held/released/refunded/disputed) are returned as-is; a
        // Pending escrow still needs a way to pay, so fall through and start its checkout.
        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (escrow != null && escrow.Status != EscrowStatus.Pending)
            return MapToResponse(escrow);

        // A checkout was already started for this escrow: ask the provider about the existing
        // reference BEFORE minting a fresh one. Overwriting a reference the tenant just paid under
        // (double-click, lost redirect) would orphan the capture wherever the webhook can't reach —
        // the verify endpoint only checks the escrow's stored reference. Simulated verifies always
        // report success, so they never count as "already paid" here.
        if (escrow is not null && !string.IsNullOrEmpty(escrow.PaymentReference))
        {
            var existing = await _paymentGateway.VerifyPaymentAsync(escrow.PaymentReference);
            if (existing.Success && !existing.Simulated)
            {
                try
                {
                    await VerifyAndHoldPaymentAsync(bookingId, escrow.PaymentReference, existing.Amount);
                    var held = await _escrowRepository.GetByBookingIdAsync(bookingId);
                    return MapToResponse(held!);
                }
                catch (InvalidOperationException ex)
                {
                    // The paid reference couldn't be applied (e.g. amount mismatch on a stale
                    // checkout) — log it and fall through to starting a fresh checkout.
                    _logger.LogWarning(ex,
                        "Existing paid reference {Reference} for booking {BookingId} could not be held; starting a new checkout",
                        escrow.PaymentReference, bookingId);
                }
            }
        }

        var isNew = escrow == null;
        // The amount is derived from the booking server-side — never trusted from the client.
        escrow ??= new Escrow
        {
            BookingId = bookingId,
            Amount = booking.TotalAmount,
            Status = EscrowStatus.Pending
        };

        // Start a Paystack checkout for the booking's tenant. The tenant always exists (the booking
        // references them and we authorized against TenantId above) — fail loudly rather than send
        // the receipt to a bogus address. Re-initiating a Pending escrow (e.g. the tenant lost the
        // checkout page) just mints a fresh reference; the webhook/verify path stamps whichever
        // reference actually gets paid.
        var tenant = await _userRepository.GetByIdAsync(booking.TenantId)
            ?? throw new InvalidOperationException("Booking tenant not found");
        var payment = await _paymentGateway.InitiatePaymentAsync(
            escrow.Amount, _platform.Currency, tenant.Email, bookingId);
        if (!payment.Success)
            throw new InvalidOperationException("The payment provider could not start the checkout. Please retry.");
        escrow.PaymentReference = payment.Reference;

        if (isNew)
            await _escrowRepository.AddAsync(escrow);
        else
            await _escrowRepository.UpdateAsync(escrow);
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

        // Same idempotency for a payment that lost the double-booking race: it was captured and
        // auto-refunded (or parked in Disputed for manual resolution). Ack the provider's retry
        // as a no-op instead of erroring, so the webhook isn't re-sent forever.
        if (escrow.Status is EscrowStatus.Refunded or EscrowStatus.Disputed && escrow.PaymentReference == reference)
        {
            _logger.LogInformation(
                "Escrow {EscrowId} already resolved as {Status} for reference {Reference} — ignoring duplicate webhook",
                escrow.Id, escrow.Status, reference);
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

        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, EscrowStatus.HeldInEscrow, actor: "payment-provider", reason: $"Payment verified (ref {reference})"));
        escrow.PaymentReference = reference;
        escrow.HeldAt = DateTime.UtcNow;

        // A paid booking is a confirmed booking. Only Confirmed bookings block availability
        // (and arm the Postgres no-overlap exclusion constraint) and only Confirmed bookings
        // can get an agreement — so this transition must land in the same save as the hold.
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
        if (booking is { Status: BookingStatus.Pending })
            booking.Status = BookingStatus.Confirmed;

        await _escrowRepository.UpdateAsync(escrow);
        try
        {
            await _escrowRepository.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // Lost the double-booking race: another booking for these dates was confirmed between
            // the availability pre-check and this save, so the no-overlap exclusion constraint
            // rejected the Confirmed flip — but the charge was already captured at the provider.
            // Refund it and void the booking instead of surfacing an opaque 500 (which would also
            // make the provider retry the webhook indefinitely).
            await ResolveLostDoubleBookingRaceAsync(escrow, booking, reference, paidAmount);
            throw new ConflictException(
                "These dates were just booked by someone else. Your payment has been refunded in full.");
        }

        _logger.LogInformation("Escrow {EscrowId} held for booking {BookingId} (reference: {Reference}); booking confirmed",
            escrow.Id, bookingId, reference);
    }

    /// <summary>
    /// Cleanup after a captured payment loses the double-booking race. The failed SaveChanges left
    /// the in-memory escrow at HeldInEscrow (accurate — the money WAS captured), so transition it
    /// on to Refunded (or Disputed if the refund fails), cancel the booking, and persist. The
    /// Pending→HeldInEscrow audit event saves alongside, keeping the trail truthful.
    /// </summary>
    private async Task ResolveLostDoubleBookingRaceAsync(Escrow escrow, Booking? booking, string reference, decimal paidAmount)
    {
        var refunded = false;
        try
        {
            refunded = await _paymentGateway.RefundAsync(reference, paidAmount);
        }
        catch (Exception refundEx)
        {
            _logger.LogError(refundEx, "Refund call failed for reference {Reference} (booking {BookingId})",
                reference, escrow.BookingId);
        }

        var reason = refunded
            ? "Double-booking race lost after capture — payment auto-refunded in full"
            : "Double-booking race lost after capture — automatic refund FAILED; resolve manually";
        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, refunded ? EscrowStatus.Refunded : EscrowStatus.Disputed, actor: "system", reason: reason));
        escrow.ReleaseReason = reason;

        if (booking is not null)
        {
            booking.Status = BookingStatus.Cancelled;
            booking.CancelledAt = DateTime.UtcNow;
        }

        await _escrowRepository.SaveChangesAsync();

        if (!refunded)
            _logger.LogError(
                "Escrow {EscrowId} marked Disputed: captured payment {Reference} lost the double-booking race and could not be auto-refunded",
                escrow.Id, reference);
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
        // A simulated verify (unconfigured gateway, dev only) can't know the real amount — use the
        // escrow's expected amount so the guard doesn't reject the simulated success as underpaid.
        await VerifyAndHoldPaymentAsync(bookingId, escrow.PaymentReference,
            result.Simulated ? escrow.Amount : result.Amount);

        var updated = await _escrowRepository.GetByBookingIdAsync(bookingId);
        return MapToResponse(updated!);
    }

    public async Task<List<EscrowResponse>> GetMyEscrowsAsync(string userId)
    {
        // The caller's bookings as a tenant, then the escrows attached to them.
        var bookings = await _bookingRepository.FindAsync(b => b.TenantId == userId);
        var bookingIds = bookings.Select(b => b.Id).ToList();
        if (bookingIds.Count == 0)
            return new List<EscrowResponse>();

        var escrows = await _escrowRepository.FindAsync(e => bookingIds.Contains(e.BookingId));
        return escrows
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => MapToResponse(e))
            .ToList();
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

        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, EscrowStatus.Released, actor: userId, reason: "Released by landlord"));
        escrow.ReleasedAt = DateTime.UtcNow;

        // Parity with the auto-release job: a released stay is a completed stay.
        // NOTE: actual disbursement to the landlord happens via Paystack Transfers, which requires a
        // verified transfer recipient per host. Until that is wired, "Released" records intent only.
        booking.Status = BookingStatus.Completed;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} released by landlord {UserId}", escrowId, userId);

        // Kick off the actual disbursement (Paystack Transfer). Idempotent, and a payout
        // hiccup never undoes the release — the payout stays visible for retry.
        await _payoutService.CreateForReleasedEscrowAsync(escrow, userId);
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

        // Only funds actually sitting in escrow can be contested. Without this guard a dispute on a
        // Released escrow could later be "resolved" into a refund — paying out the same money twice.
        if (escrow.Status != EscrowStatus.HeldInEscrow)
            throw new InvalidOperationException($"A dispute can only be raised while funds are held in escrow (current status: '{escrow.Status}')");

        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, EscrowStatus.Disputed, actor: userId, reason: reason));
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

        if (approved)
        {
            await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
                escrow, EscrowStatus.Released, actor: "admin", reason: "Dispute resolved in landlord's favour"));
            escrow.ReleasedAt = DateTime.UtcNow;

            // Parity with the release paths: a released stay is a completed stay.
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(escrow.BookingId);
            if (booking != null)
                booking.Status = BookingStatus.Completed;

            // Disburse to the host, exactly like a manual release.
            var landlordId = booking?.Property?.UserId;
            if (landlordId is not null)
            {
                await _escrowRepository.UpdateAsync(escrow);
                await _escrowRepository.SaveChangesAsync();
                await _payoutService.CreateForReleasedEscrowAsync(escrow, landlordId);
                _logger.LogInformation("Dispute resolved for escrow {EscrowId}: approved=True, new status={Status}",
                    escrowId, escrow.Status);
                return;
            }
        }
        else
        {
            // Resolving in the tenant's favour means money actually moves back — go through the
            // provider exactly like an admin refund, and only record Refunded if it accepted.
            await ExecuteProviderRefundAsync(escrow);
            await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
                escrow, EscrowStatus.Refunded, actor: "admin", reason: "Dispute resolved in tenant's favour"));
            escrow.ReleaseReason = $"Dispute resolved in tenant's favour. {escrow.ReleaseReason}".Trim();
        }

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

        await ExecuteProviderRefundAsync(escrow);

        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, EscrowStatus.Refunded, actor: "admin", reason: reason));
        escrow.ReleaseReason = reason;

        await _escrowRepository.UpdateAsync(escrow);
        await _escrowRepository.SaveChangesAsync();

        _logger.LogInformation("Escrow {EscrowId} refunded: {Reason}", escrowId, reason);
    }

    /// <summary>
    /// Issues the full-amount refund through the payment provider before anything is recorded
    /// locally — the escrow may only be marked Refunded if the provider actually accepted it,
    /// otherwise the status would lie about money that never moved. (The provider call is
    /// idempotent on the same transaction reference.)
    /// </summary>
    private async Task ExecuteProviderRefundAsync(Escrow escrow)
    {
        if (string.IsNullOrEmpty(escrow.PaymentReference))
            return;

        var refunded = await _paymentGateway.RefundAsync(escrow.PaymentReference, escrow.Amount);
        if (!refunded)
            throw new InvalidOperationException("Refund could not be processed by the payment provider. Please retry.");
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
