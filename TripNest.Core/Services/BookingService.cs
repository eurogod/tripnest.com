using TripNest.Core.DTOs.Bookings;
using TripNest.Core.Enums;
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
        try
        {
            var property = await _propertyRepository.GetByIdAsync(request.PropertyId);
            if (property == null)
                throw new InvalidOperationException("Property not found");

            // Inline overlap check — Module 19 will extract this into IAvailabilityService and add blocked-dates support
            var existingBookings = await _bookingRepository.GetByPropertyIdAsync(request.PropertyId);
            var hasOverlap = existingBookings.Any(b =>
                b.Status == BookingStatus.Confirmed &&
                b.CheckInDate < request.CheckOutDate &&
                b.CheckOutDate > request.CheckInDate);

            if (hasOverlap)
                throw new InvalidOperationException("The selected dates are not available for this property");

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

            await _bookingRepository.AddAsync(booking);
            await _bookingRepository.SaveChangesAsync();

            var escrow = new Escrow
            {
                BookingId = booking.Id,
                Amount = totalAmount,
                Status = EscrowStatus.Pending
            };

            await _escrowRepository.AddAsync(escrow);
            await _escrowRepository.SaveChangesAsync();

            _logger.LogInformation("Booking created: {BookingId}", booking.Id);

            return MapToResponse(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            throw;
        }
    }

    public async Task<BookingResponse> GetBookingAsync(string bookingId)
    {
        var booking = await _bookingRepository.GetByIdWithDetailsAsync(bookingId);
        if (booking == null)
            throw new InvalidOperationException("Booking not found");

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
        var booking = await _bookingRepository.GetByIdAsync(bookingId);
        if (booking == null)
            throw new InvalidOperationException("Booking not found");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;

        await _bookingRepository.UpdateAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        var escrow = await _escrowRepository.GetByBookingIdAsync(bookingId);
        if (escrow != null)
        {
            // TODO: Module 19 — replace always-100% refund with ICancellationPolicyService.CalculateRefundPercentage(bookingId)
            escrow.Status = EscrowStatus.Refunded;
            await _escrowRepository.UpdateAsync(escrow);
            await _escrowRepository.SaveChangesAsync();
        }

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
