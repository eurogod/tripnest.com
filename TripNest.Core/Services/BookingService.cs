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
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IPropertyRepository propertyRepository,
        IEscrowRepository escrowRepository,
        ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _propertyRepository = propertyRepository;
        _escrowRepository = escrowRepository;
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

        // Friendly pre-check. The authoritative guard against double-booking is the
        // Postgres exclusion constraint on confirmed bookings (see migration), which closes
        // the race this in-memory check cannot.
        var existingBookings = await _bookingRepository.GetByPropertyIdAsync(request.PropertyId);
        var hasOverlap = existingBookings.Any(b =>
            b.Status == BookingStatus.Confirmed &&
            b.CheckInDate < request.CheckOutDate &&
            b.CheckOutDate > request.CheckInDate);

        if (hasOverlap)
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
            // TODO: Module 19 — replace always-100% refund with ICancellationPolicyService.CalculateRefundPercentage(bookingId)
            escrow.Status = EscrowStatus.Refunded;
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
