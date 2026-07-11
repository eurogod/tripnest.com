using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Enums;
using TripNest.Core.Exceptions;
using TripNest.Core.Interfaces.Repositories;
using TripNest.Core.Interfaces.Services;
using TripNest.Core.Models;

namespace TripNest.Core.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IPropertyRepository _propertyRepository;
    private readonly IEscrowRepository _escrowRepository;
    private readonly IAvailabilityService _availabilityService;
    private readonly ICancellationPolicyService _cancellationPolicyService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IRepository<EscrowEvent> _escrowEventRepository;
    private readonly IPayoutService _payoutService;
    private readonly IRepository<PricingSettings> _pricingRepository;
    private readonly ILoyaltyService _loyaltyService;
    private readonly ISplitBillingService _splitBillingService;
    private readonly IRentService _rentService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IEscrowRepository escrowRepository,
        IAvailabilityService availabilityService,
        ICancellationPolicyService cancellationPolicyService,
        IPaymentGateway paymentGateway,
        IRepository<EscrowEvent> escrowEventRepository,
        IPayoutService payoutService,
        IRepository<PricingSettings> pricingRepository,
        ILoyaltyService loyaltyService,
        ISplitBillingService splitBillingService,
        IRentService rentService,
        ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _escrowRepository = escrowRepository;
        _availabilityService = availabilityService;
        _cancellationPolicyService = cancellationPolicyService;
        _paymentGateway = paymentGateway;
        _escrowEventRepository = escrowEventRepository;
        _payoutService = payoutService;
        _pricingRepository = pricingRepository;
        _loyaltyService = loyaltyService;
        _splitBillingService = splitBillingService;
        _rentService = rentService;
        _logger = logger;
    }

    public async Task<BookingResponse> CreateBookingAsync(string tenantId, CreateBookingRequest request)
    {
        // Booking dates are date-only by intent. Clients often send bare dates ("2026-08-04")
        // which deserialize with Kind=Unspecified — Npgsql refuses those for "timestamp with
        // time zone" columns, so normalize to UTC midnight before anything touches the database.
        var checkIn = DateTime.SpecifyKind(request.CheckInDate.Date, DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(request.CheckOutDate.Date, DateTimeKind.Utc);

        // Validate the date range before touching the database.
        if (checkOut <= checkIn)
            throw new ValidationException("Check-out date must be after the check-in date");
        if (checkIn < DateTime.UtcNow.Date)
            throw new ValidationException("Check-in date cannot be in the past");
        if (request.Guests is < 1 or > 16)
            throw new ValidationException("Guests must be between 1 and 16");

        var property = await _propertyRepository.GetByIdAsync(request.PropertyId)
            ?? throw new NotFoundException("Property");

        // Friendly pre-check (confirmed bookings + landlord-blocked dates). The authoritative
        // guard against double-booking is the Postgres exclusion constraint on confirmed
        // bookings (see migration), which closes the race this in-memory check cannot.
        if (!await _availabilityService.IsRangeAvailable(request.PropertyId, checkIn, checkOut))
            throw new ConflictException("The selected dates are not available for this property");

        // Price through the shared calculator — the same engine behind search quotes and the
        // quote endpoint — so the total charged is exactly the total the guest was shown.
        var pricing = (await _pricingRepository.FindAsync(s => s.PropertyId == request.PropertyId)).FirstOrDefault();
        if (pricing is { MinNights: > 1 } && (checkOut - checkIn).Days < pricing.MinNights)
            throw new ValidationException($"This listing requires a minimum stay of {pricing.MinNights} nights");

        var loyaltyPercent = await _loyaltyService.GetDiscountPercentAsync(tenantId);
        var totalAmount = StayPricingCalculator.Quote(property, pricing, checkIn, checkOut, loyaltyPercent).Total;

        // Long-term stays (2+ rent periods on a LongTerm/Student listing) pay monthly instead of
        // a lump sum: the upfront charge — and so the escrow and TotalAmount — covers only the
        // first 30-day period at the listing's monthly rent; the rest becomes a rent-invoice
        // schedule the tenant pays month by month. Loyalty discounts apply to short stays only.
        var nights = (checkOut - checkIn).Days;
        var monthlyBilling = property.StayType is StayType.LongTerm or StayType.Student &&
                             nights >= 2 * RentService.DaysPerPeriod;
        if (monthlyBilling)
            totalAmount = property.MonthlyRent;

        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = request.PropertyId,
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Guests = request.Guests,
            TotalAmount = totalAmount,
            Status = BookingStatus.Pending
        };

        var escrow = new Escrow
        {
            BookingId = booking.Id,
            Amount = totalAmount,
            Status = EscrowStatus.Pending
        };

        // Both repositories share the scoped DbContext, so a single SaveChanges commits the
        // booking and its escrow atomically (EF wraps it in one transaction).
        await _bookingRepository.AddAsync(booking);
        await _escrowRepository.AddAsync(escrow);

        // Group booking: split the total into per-member shares in the same transaction —
        // a booking must never exist with half its shares.
        List<Models.BookingShare>? shares = null;
        if (request.SplitWithEmails is { Count: > 0 })
        {
            if (monthlyBilling)
                throw new ValidationException(
                    "Split billing and monthly rent cannot be combined yet — book a long-term stay individually");
            shares = await _splitBillingService.BuildSharesAsync(booking, tenantId, request.SplitWithEmails);
        }

        // Monthly billing: the rent-invoice schedule for periods 2..N, committed with the booking.
        if (monthlyBilling)
            await _rentService.BuildScheduleAsync(booking, property.UserId, property.MonthlyRent);

        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking created: {BookingId}{Split}", booking.Id,
            shares is null ? "" : $" (split across {shares.Count} members)");

        var response = MapToResponse(booking);
        if (shares is not null)
            response.Shares = shares.Select(s => new BookingShareResponse
            {
                ShareId = s.Id,
                BookingId = s.BookingId,
                ParticipantUserId = s.ParticipantUserId,
                Amount = s.Amount,
                Status = s.Status
            }).ToList();
        return response;
    }

    public async Task<BookingResponse> GetBookingAsync(string bookingId, string userId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        EnsureParticipant(booking, userId);

        return MapToResponse(booking);
    }

    public async Task<IEnumerable<BookingResponse>> GetUserBookingsAsync(string tenantId)
    {
        var bookings = await _bookingRepository.GetByTenantIdAsync(tenantId);
        return bookings.Select(MapToResponse);
    }

    public async Task<BookingResponse> CancelBookingAsync(string bookingId, string userId)
    {
        // Load with details so we can authorize against the property's landlord too.
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        EnsureParticipant(booking, userId);

        if (booking.Status is BookingStatus.Cancelled or BookingStatus.Completed)
            throw new ConflictException($"A {booking.Status.ToString().ToLowerInvariant()} booking cannot be cancelled");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        // A cancelled long-term booking owes no future rent — void its unpaid invoices
        // (saved with the cancellation below; already-paid months are not clawed back here).
        await _rentService.CancelOutstandingAsync(bookingId);

        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        var retainedAmount = 0m;
        if (escrow != null)
        {
            // Tiered refund per the property's cancellation policy — but only when the TENANT
            // cancels. When the landlord pulls out, the tenant did nothing wrong and always gets
            // 100% back; docking them by their own policy would let hosts profit from cancelling.
            var cancelledByLandlord = booking.Property?.UserId == userId;
            var refundPercentage = cancelledByLandlord
                ? 100m
                : await _cancellationPolicyService.CalculateRefundPercentage(bookingId);

            if (escrow.Status == EscrowStatus.HeldInEscrow)
            {
                // Funds were actually captured — issue the partial/full refund through the provider
                // BEFORE recording it, and only mark refunded if the provider accepts it.
                var refundAmount = Math.Round(escrow.Amount * refundPercentage / 100m, 2);
                if (refundAmount > 0 && !string.IsNullOrEmpty(escrow.PaymentReference))
                {
                    var refunded = await _paymentGateway.RefundAsync(escrow.PaymentReference, refundAmount);
                    if (!refunded)
                        throw new InvalidOperationException("Refund could not be processed by the payment provider. Please retry.");
                }

                var refundReason = $"Cancelled — {refundPercentage:0}% refund (GH₵{Math.Round(escrow.Amount * refundPercentage / 100m, 2)}) per cancellation policy";
                await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
                    escrow, EscrowStatus.Refunded, actor: userId, reason: refundReason));
                escrow.ReleaseReason = refundReason;

                // Whatever the policy did NOT refund belongs to the host — without a payout it
                // would sit in the platform's provider balance unrecorded and never be disbursed.
                retainedAmount = Math.Round(escrow.Amount - refundAmount, 2);
            }
            else if (escrow.Status == EscrowStatus.Pending)
            {
                // No money was ever captured; just void the pending escrow — no provider call.
                await _escrowEventRepository.AddAsync(EscrowStateMachine.Transition(
                    escrow, EscrowStatus.Refunded, actor: userId, reason: "Cancelled before payment was captured"));
                escrow.ReleaseReason = "Cancelled before payment was captured";
            }
            // Released/Refunded/Disputed escrows are left untouched here (handled via the dispute flow).
        }

        // Single atomic commit for the booking + its escrow (shared DbContext).
        await _bookingRepository.SaveChangesAsync();

        // Disburse the host's retained share of a partial refund. After the cancellation commit:
        // the payout is idempotent and never throws, so a payout hiccup can't undo the
        // cancellation — it just stays Pending/Failed for retry.
        var retainedForLandlordId = booking.Property?.UserId;
        if (escrow != null && retainedAmount > 0 && retainedForLandlordId != null)
            await _payoutService.CreateForReleasedEscrowAsync(escrow, retainedForLandlordId, retainedAmount);

        _logger.LogInformation("Booking {BookingId} cancelled by {UserId}", bookingId, userId);

        return MapToResponse(booking);
    }

    /// <summary>Throws Forbidden unless the user is the booking's tenant or the property's landlord.</summary>
    private static void EnsureParticipant(Booking booking, string userId)
    {
        var landlordId = booking.Property?.UserId;
        if (booking.TenantId != userId && landlordId != userId)
            throw new ForbiddenException("You do not have access to this booking");
    }

    private BookingResponse MapToResponse(Booking booking)
    {
        return new BookingResponse
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            Guests = booking.Guests,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt
        };
    }
}
