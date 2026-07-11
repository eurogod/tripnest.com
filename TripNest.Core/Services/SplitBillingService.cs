using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;
using TripNest.Core.Options;

namespace TripNest.Core.Services;

/// <summary>
/// Group bookings with unified split billing. The booking's total is divided into per-member
/// shares at creation; each member pays their own share through their own provider checkout
/// (reference metadata "share:{id}" routes their webhook here instead of the whole-escrow path).
/// Only when EVERY share is paid does the escrow hold and the booking confirm — one member's
/// payment is never at risk of covering another's. A group that misses the payment window
/// (Booking:SplitPaymentWindowHours, default 24) has its booking cancelled and paid shares
/// refunded in full.
/// </summary>
public class SplitBillingService : ISplitBillingService
{
    /// <summary>Prefix marking a provider metadata bookingId as a share charge.</summary>
    public const string ReferencePrefix = "share:";

    private readonly IRepository<BookingShare> _shareRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IRepository<EscrowEvent> _escrowEventRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IPaymentGateway _paymentGateway;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly PlatformOptions _platform;
    private readonly ILogger<SplitBillingService> _logger;

    public SplitBillingService(
        IRepository<BookingShare> shareRepository,
        IBookingRepository bookingRepository,
        IEscrowRepository escrowRepository,
        IRepository<EscrowEvent> escrowEventRepository,
        IUserRepository userRepository,
        IPropertyRepository propertyRepository,
        IPaymentGateway paymentGateway,
        INotificationService notificationService,
        IConfiguration configuration,
        IOptions<PlatformOptions> platformOptions,
        ILogger<SplitBillingService> logger)
    {
        _shareRepository = shareRepository;
        _bookingRepository = bookingRepository;
        _escrowRepository = escrowRepository;
        _escrowEventRepository = escrowEventRepository;
        _userRepository = userRepository;
        _propertyRepository = propertyRepository;
        _paymentGateway = paymentGateway;
        _notificationService = notificationService;
        _configuration = configuration;
        _platform = platformOptions.Value;
        _logger = logger;
    }

    private int WindowHours => _configuration.GetValue("Booking:SplitPaymentWindowHours", 24);

    public async Task<List<BookingShare>> BuildSharesAsync(Booking booking, string bookerId, List<string> coTravellerEmails)
    {
        var emails = coTravellerEmails
            .Select(e => e.Trim())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (emails.Count == 0)
            throw new ValidationException("Provide at least one co-traveller email to split the bill");
        if (emails.Count > 15)
            throw new ValidationException("A group booking supports at most 16 members");

        var members = new List<User>();
        foreach (var email in emails)
        {
            var user = await _userRepository.GetByEmailAsync(email)
                ?? throw new ValidationException($"No TripNest account found for '{email}' — everyone in the group needs an account first");
            if (user.Id == bookerId)
                continue; // the booker always has a share; listing themselves is harmless
            members.Add(user);
        }
        if (members.Count == 0)
            throw new ValidationException("The co-travellers are all you — book normally instead");

        // Equal split at 2 decimals; the booker's share absorbs the rounding remainder so the
        // shares always sum to exactly the booking total.
        var headCount = members.Count + 1;
        var memberShare = Math.Round(booking.TotalAmount / headCount, 2);
        var bookerShare = booking.TotalAmount - memberShare * members.Count;

        var shares = new List<BookingShare>
        {
            new() { BookingId = booking.Id, ParticipantUserId = bookerId, Amount = bookerShare }
        };
        shares.AddRange(members.Select(m => new BookingShare
        {
            BookingId = booking.Id,
            ParticipantUserId = m.Id,
            Amount = memberShare
        }));

        foreach (var share in shares)
            await _shareRepository.AddAsync(share);

        // In-app nudge for each co-traveller; the booking is only held while everyone pays.
        foreach (var member in members)
            await _notificationService.CreateAsync(
                member.Id,
                "You've been added to a group booking",
                $"Pay your share of GHS {memberShare:0.00} within {WindowHours} hours to confirm the stay.",
                booking.Id, "Booking");

        return shares;
    }

    public async Task<List<BookingShareResponse>> GetSharesAsync(string bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");
        var shares = (await _shareRepository.FindAsync(s => s.BookingId == bookingId))
            .OrderBy(s => s.CreatedAt).ToList();
        if (shares.Count == 0)
            throw new NotFoundException("Booking shares");

        // Visible to the group and the property's landlord — nobody else.
        var landlordId = booking.Property?.UserId
            ?? (await _propertyRepository.GetByIdAsync(booking.PropertyId))?.UserId;
        if (userId != landlordId && shares.All(s => s.ParticipantUserId != userId))
            throw new ForbiddenException("You are not part of this booking");

        var userIds = shares.Select(s => s.ParticipantUserId).Distinct().ToList();
        var names = (await _userRepository.FindAsync(u => userIds.Contains(u.Id)))
            .ToDictionary(u => u.Id, u => u.FullName);
        return shares.Select(s => Map(s, names.GetValueOrDefault(s.ParticipantUserId))).ToList();
    }

    public async Task<BookingShareResponse> InitiateSharePaymentAsync(string shareId, string userId)
    {
        var (share, booking) = await LoadOwnShareAsync(shareId, userId);

        if (share.Status == BookingShareStatus.Paid)
            return Map(share);
        if (booking.Status != BookingStatus.Pending)
            throw new ValidationException("This booking is no longer awaiting payment");
        if (IsWindowElapsed(booking))
        {
            await ExpireBookingAsync(booking);
            await _shareRepository.SaveChangesAsync();
            throw new ValidationException("The group payment window has elapsed — the booking was cancelled and any paid shares refunded");
        }

        var participant = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("User");

        // "share:{id}" in the provider metadata routes the success webhook to the share path.
        var payment = await _paymentGateway.InitiatePaymentAsync(
            share.Amount, _platform.Currency, participant.Email, $"{ReferencePrefix}{share.Id}");
        if (!payment.Success)
            throw new ValidationException("The payment provider could not start the checkout. Please retry.");

        share.PaymentReference = payment.Reference;
        await _shareRepository.UpdateAsync(share);
        await _shareRepository.SaveChangesAsync();

        var response = Map(share);
        response.CheckoutUrl = payment.CheckoutUrl;
        return response;
    }

    public async Task<BookingShareResponse> VerifySharePaymentAsync(string shareId, string userId)
    {
        var (share, _) = await LoadOwnShareAsync(shareId, userId);
        if (share.Status == BookingShareStatus.Paid)
            return Map(share);
        if (string.IsNullOrEmpty(share.PaymentReference))
            throw new ValidationException("No payment has been started for this share yet");

        var result = await _paymentGateway.VerifyPaymentAsync(share.PaymentReference);
        if (!result.Success)
            throw new ValidationException("Payment has not been completed for this share yet");

        // A simulated verify (dev-only gateway) can't know the amount — substitute the expected one.
        await ApplySharePaymentAsync(share.Id, share.PaymentReference, result.Simulated ? share.Amount : result.Amount);
        var refreshed = await _shareRepository.GetByIdAsync(share.Id);
        return Map(refreshed!);
    }

    public async Task ApplySharePaymentAsync(string shareId, string reference, decimal paidAmount)
    {
        var share = await _shareRepository.GetByIdAsync(shareId)
            ?? throw new InvalidOperationException($"Booking share '{shareId}' not found");

        // Providers retry webhooks — an already-paid share is a successful no-op.
        if (share.Status == BookingShareStatus.Paid)
            return;

        var booking = await _bookingRepository.GetByIdWithDetailsAsync(share.BookingId)
            ?? throw new InvalidOperationException($"Booking '{share.BookingId}' not found");

        // A charge that lands after expiry/cancellation is real money with no stay — refund it.
        if (booking.Status != BookingStatus.Pending)
        {
            _logger.LogWarning("Share {ShareId} paid (ref {Reference}) but booking {BookingId} is {Status} — refunding",
                shareId, reference, booking.Id, booking.Status);
            if (!await _paymentGateway.RefundAsync(reference, paidAmount))
                throw new InvalidOperationException($"Late share payment '{reference}' could not be refunded — manual reconciliation required");
            return;
        }

        // Amount guard, same 1-pesewa tolerance as the whole-escrow path.
        if (Math.Abs(paidAmount - share.Amount) > 0.01m)
            throw new InvalidOperationException(
                $"Paid amount ({paidAmount:0.00}) does not match this share ({share.Amount:0.00})");

        share.Status = BookingShareStatus.Paid;
        share.PaymentReference = reference;
        share.PaidAt = DateTime.UtcNow;
        await _shareRepository.UpdateAsync(share);

        var allShares = (await _shareRepository.FindAsync(s => s.BookingId == booking.Id)).ToList();
        var unpaid = allShares.Count(s => s.Id != share.Id && s.Status != BookingShareStatus.Paid);
        if (unpaid > 0)
        {
            await _shareRepository.SaveChangesAsync();
            _logger.LogInformation("Share {ShareId} paid for booking {BookingId}; {Unpaid} share(s) outstanding",
                shareId, booking.Id, unpaid);
            return;
        }

        await HoldEscrowForCompletedGroupAsync(booking, allShares);
    }

    public async Task ExpireOverdueAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddHours(-WindowHours);
        // Overdue group bookings = still Pending, have shares, created before the cutoff.
        var overdueShares = await _shareRepository.FindAsync(s =>
            s.Booking!.Status == BookingStatus.Pending && s.Booking.CreatedAt < cutoff);
        var bookingIds = overdueShares.Select(s => s.BookingId).Distinct().ToList();

        foreach (var bookingId in bookingIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
            if (booking is not { Status: BookingStatus.Pending })
                continue;
            await ExpireBookingAsync(booking);
        }

        if (bookingIds.Count > 0)
            await _shareRepository.SaveChangesAsync();
    }

    /// <summary>
    /// The last share just landed: hold the escrow under a synthetic group reference and confirm
    /// the booking — the same state transition, audit event, and double-booking-race handling as
    /// the whole-escrow path, except a lost race refunds every member's own charge.
    /// </summary>
    private async Task HoldEscrowForCompletedGroupAsync(Booking booking, List<BookingShare> allShares)
    {
        var escrow = await _escrowRepository.GetByBookingIdAsync(booking.Id)
            ?? throw new InvalidOperationException($"No escrow found for booking '{booking.Id}'");
        if (escrow.Status != EscrowStatus.Pending)
            throw new InvalidOperationException($"Escrow cannot be held from status '{escrow.Status}'");

        var groupReference = $"{ReferencePrefix}{booking.Id}";
        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, EscrowStatus.HeldInEscrow, actor: "payment-provider",
            reason: $"All {allShares.Count} split shares paid (group ref {groupReference})"));
        escrow.PaymentReference = groupReference;
        escrow.HeldAt = DateTime.UtcNow;
        booking.Status = BookingStatus.Confirmed;

        await _escrowRepository.UpdateAsync(escrow);
        try
        {
            await _escrowRepository.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            // Lost the double-booking race after the group finished paying: refund every member.
            await ResolveLostRaceAsync(booking, escrow, allShares);
            throw new ConflictException(
                "These dates were just booked by someone else. Every member's payment has been refunded in full.");
        }

        _logger.LogInformation("Escrow {EscrowId} held for group booking {BookingId} ({Members} members); booking confirmed",
            escrow.Id, booking.Id, allShares.Count);
    }

    private async Task ResolveLostRaceAsync(Booking booking, Escrow escrow, List<BookingShare> allShares)
    {
        var allRefunded = await RefundPaidSharesAsync(allShares);
        var reason = allRefunded
            ? "Group booking lost the double-booking race — every member's share auto-refunded"
            : "Group booking lost the double-booking race — one or more share refunds FAILED; resolve manually";

        await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
            escrow, allRefunded ? EscrowStatus.Refunded : EscrowStatus.Disputed, actor: "system", reason: reason));
        escrow.ReleaseReason = reason;
        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        await _escrowRepository.SaveChangesAsync();

        if (!allRefunded)
            _logger.LogError("Escrow {EscrowId} marked Disputed: group share refunds incomplete for booking {BookingId}",
                escrow.Id, booking.Id);
    }

    /// <summary>Cancels an overdue group booking and refunds whoever already paid.</summary>
    private async Task ExpireBookingAsync(Booking booking)
    {
        var shares = (await _shareRepository.FindAsync(s => s.BookingId == booking.Id)).ToList();
        var allRefunded = await RefundPaidSharesAsync(shares);

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        await _bookingRepository.UpdateAsync(booking);

        // The escrow never held (stays Pending), but record the outcome on its audit trail.
        var escrow = await _escrowRepository.GetByBookingIdAsync(booking.Id);
        if (escrow is not null)
            await _escrowEventRepository.AddAsync(new EscrowEvent
            {
                EscrowId = escrow.Id,
                BookingId = booking.Id,
                FromStatus = escrow.Status,
                ToStatus = escrow.Status,
                Actor = "system",
                Reason = allRefunded
                    ? $"Group payment window ({WindowHours}h) elapsed — booking cancelled, paid shares refunded"
                    : $"Group payment window ({WindowHours}h) elapsed — booking cancelled; one or more share refunds FAILED, resolve manually"
            });

        foreach (var share in shares)
            await _notificationService.CreateAsync(
                share.ParticipantUserId,
                "Group booking cancelled",
                "Not everyone paid their share in time, so the booking was cancelled. Anything you paid has been refunded.",
                booking.Id, "Booking");

        _logger.LogInformation("Group booking {BookingId} expired after {Hours}h window; {Paid} paid share(s) refunded",
            booking.Id, WindowHours, shares.Count(s => s.Status == BookingShareStatus.Refunded));
    }

    /// <summary>Refunds every Paid share at the provider, marking successes Refunded. Returns
    /// false if any refund failed (those shares stay Paid for manual reconciliation).</summary>
    private async Task<bool> RefundPaidSharesAsync(List<BookingShare> shares)
    {
        var allRefunded = true;
        foreach (var share in shares.Where(s => s.Status == BookingShareStatus.Paid))
        {
            var refunded = false;
            try
            {
                refunded = !string.IsNullOrEmpty(share.PaymentReference) &&
                           await _paymentGateway.RefundAsync(share.PaymentReference, share.Amount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refund call failed for share {ShareId} (ref {Reference})",
                    share.Id, share.PaymentReference);
            }

            if (refunded)
            {
                share.Status = BookingShareStatus.Refunded;
                await _shareRepository.UpdateAsync(share);
            }
            else
            {
                allRefunded = false;
                _logger.LogError("Share {ShareId} (ref {Reference}, {Amount}) could not be refunded — manual reconciliation required",
                    share.Id, share.PaymentReference, share.Amount);
            }
        }
        return allRefunded;
    }

    private async Task<(BookingShare Share, Booking Booking)> LoadOwnShareAsync(string shareId, string userId)
    {
        var share = await _shareRepository.GetByIdAsync(shareId)
            ?? throw new NotFoundException("Booking share");
        if (share.ParticipantUserId != userId)
            throw new ForbiddenException("This share belongs to another group member");
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(share.BookingId)
            ?? throw new NotFoundException("Booking");
        return (share, booking);
    }

    private bool IsWindowElapsed(Booking booking) =>
        WindowHours > 0 && booking.CreatedAt < DateTime.UtcNow.AddHours(-WindowHours);

    private static BookingShareResponse Map(BookingShare share, string? participantName = null) => new()
    {
        ShareId = share.Id,
        BookingId = share.BookingId,
        ParticipantUserId = share.ParticipantUserId,
        ParticipantName = participantName,
        Amount = share.Amount,
        Status = share.Status,
        PaidAt = share.PaidAt
    };
}
