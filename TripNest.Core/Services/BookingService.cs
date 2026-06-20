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
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IEscrowRepository escrowRepository,
        IAvailabilityService availabilityService,
        ICancellationPolicyService cancellationPolicyService,
        ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _escrowRepository = escrowRepository;
        _availabilityService = availabilityService;
        _cancellationPolicyService = cancellationPolicyService;
        _logger = logger;
    }

    public async Task<BookingResponse> CreateBookingAsync(string tenantId, CreateBookingRequest request)
    {
        // Validate the date range before touching the database.
        if (request.CheckOutDate.Date <= request.CheckInDate.Date)
            throw new ValidationException("Check-out date must be after the check-in date");
        if (request.CheckInDate.Date < DateTime.UtcNow.Date)
            throw new ValidationException("Check-in date cannot be in the past");

        var property = await _propertyRepository.GetByIdAsync(request.PropertyId)
            ?? throw new NotFoundException("Property");

        // Friendly pre-check (confirmed bookings + landlord-blocked dates). The authoritative
        // guard against double-booking is the Postgres exclusion constraint on confirmed
        // bookings (see migration), which closes the race this in-memory check cannot.
        if (!await _availabilityService.IsRangeAvailable(request.PropertyId, request.CheckInDate, request.CheckOutDate))
            throw new ConflictException("The selected dates are not available for this property");

        var totalAmount = CalculateTotalAmount(property, request.CheckInDate, request.CheckOutDate);

        var booking = new Booking
        {
            TenantId = tenantId,
            PropertyId = request.PropertyId,
            CheckInDate = request.CheckInDate,
            CheckOutDate = request.CheckOutDate,
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
        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking created: {BookingId}", booking.Id);

        return MapToResponse(booking);
    }

    public async Task<BookingResponse> GetBookingAsync(string bookingId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        return MapToResponse(booking);
    }

    public async Task<IEnumerable<BookingResponse>> GetUserBookingsAsync(string tenantId)
    {
        var bookings = await _bookingRepository.GetByTenantIdAsync(tenantId);
        return bookings.Select(MapToResponse);
    }

    public async Task<IEnumerable<BookingResponse>> GetPropertyBookingsAsync(string propertyId)
    {
        var bookings = await _bookingRepository.GetByPropertyIdAsync(propertyId);
        return bookings.Select(MapToResponse);
    }

    public async Task<BookingResponse> CancelBookingAsync(string bookingId)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId)
            ?? throw new NotFoundException("Booking");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (escrow != null)
        {
            // Tiered refund based on the property's cancellation policy and how far out check-in is.
            var refundPercentage = await _cancellationPolicyService.CalculateRefundPercentage(bookingId);
            escrow.Status = EscrowStatus.Refunded;
            escrow.ReleaseReason = $"Cancelled — {refundPercentage:0}% refund per cancellation policy";
        }

        // Single atomic commit for the booking + its escrow (shared DbContext).
        await _bookingRepository.SaveChangesAsync();

        return MapToResponse(booking);
    }

    private decimal CalculateTotalAmount(Property property, DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut - checkIn).Days;
        return (property.DailyRate ?? (property.MonthlyRent / 30)) * nights;
    }

    private BookingResponse MapToResponse(Booking booking)
    {
        return new BookingResponse
        {
            BookingId = booking.Id,
            PropertyId = booking.PropertyId,
            CheckInDate = booking.CheckInDate,
            CheckOutDate = booking.CheckOutDate,
            TotalAmount = booking.TotalAmount,
            Status = booking.Status,
            CreatedAt = booking.CreatedAt
        };
    }
}
